using NotificationService.Data;
using NotificationService.Messaging;
using NotificationService.Services;
using Serilog;
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
    Log.Information("Starting NotificationService");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── Health checks ─────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    // ── MongoDB support for notifications ─────────────────────────────────────
    var mongoConnection = builder.Configuration["MongoDb:ConnectionString"] ?? "mongodb://mongo:27017";
    var mongoDatabase = builder.Configuration["MongoDb:Database"] ?? "notificationsdb";
    builder.Services.AddSingleton(new MongoNotificationContext(mongoConnection, mongoDatabase));
    builder.Services.AddScoped<NotificationStore>();

    // ── RabbitMQ consumers ────────────────────────────────────────────────────
    var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";

    builder.Services.AddHostedService(sp => new OrderConfirmedConsumer(
        rabbitHost,
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<OrderConfirmedConsumer>>()));

    builder.Services.AddHostedService(sp => new OrderCancelledConsumer(
        rabbitHost,
        sp.GetRequiredService<IServiceScopeFactory>(),
        sp.GetRequiredService<ILogger<OrderCancelledConsumer>>()));

    var app = builder.Build();

    app.UseCorrelationId();
    app.UseRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("NotificationService is running");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "NotificationService terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
