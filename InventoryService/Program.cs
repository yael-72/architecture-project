using InventoryService.Data;
using InventoryService.Interfaces;
using InventoryService.Messaging;
using InventoryService.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Serilog;
using SharedKernel.Auth;
using SharedKernel.Messaging;
using SharedKernel.Middleware;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    Log.Information("Starting InventoryService");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory Service", Version = "v1" });
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

    builder.Services.AddDbContext<InventoryDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sql => sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = JwtHelper.GetValidationParameters(builder.Configuration);
        });
    builder.Services.AddAuthorization();

    builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
    builder.Services.AddScoped<IInventoryService, InventoryService.Services.InventoryService>();

    // ── RabbitMQ messaging ────────────────────────────────────────────────────
    var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";

    builder.Services.AddSingleton<IMessagePublisher>(sp =>
        new RabbitMqPublisher(rabbitHost, sp.GetRequiredService<ILogger<RabbitMqPublisher>>()));

    // Saga consumers (BackgroundServices — start once and keep listening)
    builder.Services.AddHostedService(sp => new InventoryOrderPlacedConsumer(
        rabbitHost,
        sp.GetRequiredService<IMessagePublisher>(),
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<InventoryOrderPlacedConsumer>>()));

    builder.Services.AddHostedService(sp => new InventoryOrderCancelledConsumer(
        rabbitHost,
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<InventoryOrderCancelledConsumer>>()));

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!);

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
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

    Log.Information("InventoryService is running");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "InventoryService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
