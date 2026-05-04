using Serilog.Context;

namespace Scheduler.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx)
    {
        var id = ctx.Request.Headers.TryGetValue(HeaderName, out var hv) && !string.IsNullOrWhiteSpace(hv)
            ? hv.ToString()
            : Guid.NewGuid().ToString("N");

        ctx.Response.Headers[HeaderName] = id;
        ctx.Items[HeaderName] = id;
        using (LogContext.PushProperty("CorrelationId", id))
        {
            await _next(ctx);
        }
    }
}
