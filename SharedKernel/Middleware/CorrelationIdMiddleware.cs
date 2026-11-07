using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace SharedKernel.Middleware;

/// <summary>
/// Reads X-Correlation-Id from the incoming request (or generates a new one),
/// stores it in CorrelationContext, adds it to the response header,
/// and pushes it into the Serilog LogContext so every log entry includes it.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        Guid correlationId;

        if (context.Request.Headers.TryGetValue(HeaderName, out var existing) &&
            Guid.TryParse(existing, out var parsed))
        {
            correlationId = parsed;
        }
        else
        {
            correlationId = Guid.NewGuid();
        }

        CorrelationContext.CorrelationId = correlationId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
                context.Response.Headers[HeaderName] = correlationId.ToString();
            return Task.CompletedTask;
        });

        // Push into Serilog so {CorrelationId} appears in every log line for this request
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
