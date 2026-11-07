using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SharedKernel.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    private static readonly HashSet<string> SkipPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/health", "/healthz", "/swagger", "/favicon.ico"
    };

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        if (ShouldSkipLogging(path))
        {
            await _next(context);
            return;
        }

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
            sw.Stop();

            var status = context.Response.StatusCode;
            var ms = sw.ElapsedMilliseconds;

            if (status >= 400 || ms > 500)
                _logger.LogWarning("{Method} {Path} responded {Status} in {Ms}ms", method, path, status, ms);
            else
                _logger.LogDebug("{Method} {Path} responded {Status} in {Ms}ms", method, path, status, ms);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Unhandled exception during {Method} {Path} after {Ms}ms", method, path, sw.ElapsedMilliseconds);

            var statusCode = ex switch
            {
                ArgumentException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                UnauthorizedAccessException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError
            };

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                status = statusCode,
                detail = ex.Message,
                traceId = context.TraceIdentifier
            });
        }
    }

    private static bool ShouldSkipLogging(string path) =>
        path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase) ||
        SkipPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
