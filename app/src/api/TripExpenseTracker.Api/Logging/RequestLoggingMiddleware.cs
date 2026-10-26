namespace TripExpenseTracker.Api.Logging;

/// <summary>
/// Writes one line per request: method, path and status code. The query string, headers and bodies are never
/// logged (F1.R11). It uses the injected logger, not the static Serilog logger, so each host logs to its own sinks.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        finally
        {
            _logger.LogInformation(
                "HTTP {RequestMethod} {RequestPath} responded {StatusCode}",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode);
        }
    }
}
