using Microsoft.AspNetCore.Builder;

namespace SharedKernel.Middleware;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder builder) =>
        builder.UseMiddleware<RequestLoggingMiddleware>();

    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder builder) =>
        builder.UseMiddleware<RateLimitingMiddleware>();

    /// <summary>
    /// Reads (or generates) X-Correlation-Id, stores it in CorrelationContext,
    /// echoes it in the response header, and enriches the Serilog LogContext.
    /// Register this BEFORE UseRequestLogging so every log line carries the ID.
    /// </summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder) =>
        builder.UseMiddleware<CorrelationIdMiddleware>();
}
