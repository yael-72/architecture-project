using BffService.Clients;
using BffService.Composers;
using Serilog;
using Serilog.Events;
using SharedKernel.Auth;
using SharedKernel.Middleware;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, config) =>
        config.ReadFrom.Configuration(context.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext()
              .WriteTo.Console()
              .WriteTo.File("Logs/log-.txt", rollingInterval: RollingInterval.Day));

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
            options.JsonSerializerOptions.ReferenceHandler =
                System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var jwtSection = builder.Configuration.GetSection("Jwt");
    builder.Services.AddAuthentication("Bearer")
        .AddJwtBearer("Bearer", options =>
        {
            options.TokenValidationParameters = JwtHelper.GetValidationParameters(
                jwtSection["Secret"]!,
                jwtSection["Issuer"]!,
                jwtSection["Audience"]!);
        });
    builder.Services.AddAuthorization();

    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "Bff:";
    });

    var serviceUrls = builder.Configuration.GetSection("ServiceUrls");

    builder.Services.AddHttpClient<CatalogClient>(client =>
        client.BaseAddress = new Uri(serviceUrls["CatalogService"]!));

    builder.Services.AddHttpClient<IOrderClient, OrderClient>(client =>
        client.BaseAddress = new Uri(serviceUrls["OrderService"]!));

    builder.Services.AddHttpClient<IUserClient, UserClient>(client =>
        client.BaseAddress = new Uri(serviceUrls["UserAuthService"]!));

    builder.Services.AddScoped<ICatalogClient, CachedCatalogClient>();
    builder.Services.AddScoped<ProductWithOrdersComposer>();

    builder.Services.AddHealthChecks();

    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCorrelationId();
    // Rate limiting is handled at the ApiGateway level.
    app.UseRequestLogging();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("BffService is running");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "BffService failed to start");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
