using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Serilog;
using SharedKernel.Auth;
using SharedKernel.Caching;
using SharedKernel.Middleware;
using StackExchange.Redis;
using ProductCatalogService.Data;
using ProductCatalogService.Interfaces;
using ProductCatalogService.Repositories;
using ProductCatalogService.Services;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting ProductCatalogService");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "ProductCatalog Service", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddDbContext<CatalogDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var redisConfig = ConfigurationOptions.Parse(redisConnectionString);
    redisConfig.AbortOnConnectFail = false;
    var redisMultiplexer = ConnectionMultiplexer.Connect(redisConfig);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.ConnectionMultiplexerFactory = () => Task.FromResult((IConnectionMultiplexer)redisMultiplexer);
        options.InstanceName = builder.Configuration.GetValue<string>("Cache:InstanceName", "Catalog:");
    });
    builder.Services.AddSingleton<ICacheService, CacheService>();

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = JwtHelper.GetValidationParameters(builder.Configuration);
        });
    builder.Services.AddAuthorization();

    builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();
    builder.Services.AddScoped<IProductRepository, ProductRepository>();

    builder.Services.AddScoped<CategoryService>();
    builder.Services.AddScoped<ProductService>();

    builder.Services.AddScoped<ICategoryService>(sp => new CachedCategoryService(
        sp.GetRequiredService<CategoryService>(),
        sp.GetRequiredService<ICacheService>(),
        sp.GetRequiredService<ILogger<CachedCategoryService>>(),
        sp.GetRequiredService<IConfiguration>()));

    builder.Services.AddScoped<IProductService>(sp => new CachedProductService(
        sp.GetRequiredService<ProductService>(),
        sp.GetRequiredService<ICacheService>(),
        sp.GetRequiredService<ILogger<CachedProductService>>(),
        sp.GetRequiredService<IConfiguration>()));

    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!)
        .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

    var app = builder.Build();

    // Retry migration to handle race condition when multiple replicas start simultaneously
    for (int attempt = 1; attempt <= 5; attempt++)
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            db.Database.Migrate();
            break;
        }
        catch (Exception ex) when (attempt < 5)
        {
            var logger = app.Services.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("Migration attempt {Attempt} failed: {Message}. Retrying in 5s...", attempt, ex.Message);
            Thread.Sleep(5000);
        }
    }

    app.UseCorrelationId();
    app.UseRequestLogging();
    app.UseRateLimiting();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("ProductCatalogService is running");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "ProductCatalogService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
