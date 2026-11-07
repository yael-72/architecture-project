using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SharedKernel.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConnectionMultiplexer _redis;
    private readonly int _requestLimit;
    private readonly TimeSpan _timeWindow;

    public RateLimitingMiddleware(
        RequestDelegate next,
        ILogger<RateLimitingMiddleware> logger,
        IConnectionMultiplexer redis,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _redis = redis;
        _requestLimit = configuration.GetValue<int>("RateLimiting:RequestLimit", 100);
        _timeWindow = TimeSpan.FromMinutes(configuration.GetValue<int>("RateLimiting:TimeWindowMinutes", 1));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var clientId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var key = $"ratelimit:{clientId}";
        var db = _redis.GetDatabase();

        var count = await db.StringIncrementAsync(key);
        if (count == 1) await db.KeyExpireAsync(key, _timeWindow);

        var ttl = await db.KeyTimeToLiveAsync(key);
        var retryAfter = (int)(ttl?.TotalSeconds ?? _timeWindow.TotalSeconds);

        if (count > _requestLimit)
        {
            _logger.LogWarning("Rate limit exceeded for client: {ClientId}", clientId);
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = retryAfter.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Rate limit exceeded. Please try again later.",
                statusCode = 429,
                retryAfter = $"{retryAfter} seconds"
            });
            return;
        }

        context.Response.Headers["X-Rate-Limit-Limit"] = _requestLimit.ToString();
        context.Response.Headers["X-Rate-Limit-Remaining"] = Math.Max(0, _requestLimit - (int)count).ToString();

        await _next(context);
    }
}
