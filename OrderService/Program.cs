using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Serilog;
using SharedKernel.Auth;
using SharedKernel.Messaging;
using SharedKernel.Middleware;
using StackExchange.Redis;
using OrderService.Clients;
using OrderService.Data;
using OrderService.Interfaces;
using OrderService.Messaging;
using OrderService.Repositories;
using OrderService.Services;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting OrderService");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Order Service", Version = "v1" });
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization", Type = SecuritySchemeType.Http,
            Scheme = "Bearer", BearerFormat = "JWT", In = ParameterLocation.Header
        });
        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddDbContext<OrderDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

    // Redis for caching product lookups during order creation
    var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var redisConfig = ConfigurationOptions.Parse(redisConnectionString);
    redisConfig.AbortOnConnectFail = false;
    var redisMultiplexer = ConnectionMultiplexer.Connect(redisConfig);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redisMultiplexer);
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.ConnectionMultiplexerFactory = () => Task.FromResult((IConnectionMultiplexer)redisMultiplexer);
        options.InstanceName = "Order:";
    });

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = JwtHelper.GetValidationParameters(builder.Configuration);
        });
    builder.Services.AddAuthorization();

    // Register typed HTTP clients pointing to downstream services
    builder.Services.AddHttpClient<CatalogClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:CatalogService"] ?? "http://localhost:5002/");
    });
    builder.Services.AddHttpClient<IUserClient, UserClient>(client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["ServiceUrls:UserAuthService"] ?? "http://localhost:5001/");
    });

    // Wrap CatalogClient with caching decorator
    builder.Services.AddScoped<ICatalogClient>(sp => new CachedCatalogClient(
        sp.GetRequiredService<CatalogClient>(),
        sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
        sp.GetRequiredService<ILogger<CachedCatalogClient>>()));

    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();

    // ── RabbitMQ messaging ────────────────────────────────────────────────────
    var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";

    builder.Services.AddSingleton<IMessagePublisher>(sp =>
        new RabbitMqPublisher(rabbitHost, sp.GetRequiredService<ILogger<RabbitMqPublisher>>()));

    // Saga consumers (react to InventoryService responses)
    builder.Services.AddHostedService(sp => new InventoryReservedConsumer(
        rabbitHost,
        sp.GetRequiredService<IMessagePublisher>(),
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<InventoryReservedConsumer>>()));

    builder.Services.AddHostedService(sp => new InventoryRejectedConsumer(
        rabbitHost,
        sp.GetRequiredService<IMessagePublisher>(),
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<InventoryRejectedConsumer>>()));

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        db.Database.Migrate();
    }

    app.UseCorrelationId();
    app.UseRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("OrderService is running");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrderService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
