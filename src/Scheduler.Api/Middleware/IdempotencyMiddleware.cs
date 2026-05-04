using System.Security.Cryptography;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Api.Middleware;

public sealed class IdempotencyMiddleware
{
    public const string HeaderName = "Idempotency-Key";
    private readonly RequestDelegate _next;

    public IdempotencyMiddleware(RequestDelegate next) => _next = next;

    public async Task Invoke(HttpContext ctx, IIdempotencyStore store)
    {
        if (!IsBookingMutation(ctx) || !ctx.Request.Headers.TryGetValue(HeaderName, out var keyHeader))
        {
            await _next(ctx);
            return;
        }

        var key = keyHeader.ToString();
        ctx.Request.EnableBuffering();
        var bodyHash = await ComputeBodyHashAsync(ctx);

        var hit = await store.TryGetAsync(key, bodyHash, ctx.RequestAborted);
        if (hit is not null)
        {
            ctx.Response.StatusCode = int.Parse(hit.ResponseStatusCode);
            ctx.Response.ContentType = "application/json";
            await ctx.Response.WriteAsync(hit.ResponseBody);
            return;
        }

        if (await store.ExistsForDifferentBodyAsync(key, bodyHash, ctx.RequestAborted))
        {
            throw new IdempotencyReplayMismatchException();
        }

        ctx.Items["IdempotencyKey"] = key;
        ctx.Items["IdempotencyBodyHash"] = bodyHash;
        await _next(ctx);
    }

    private static bool IsBookingMutation(HttpContext ctx) =>
        ctx.Request.Method == "POST" &&
        ctx.Request.Path.StartsWithSegments("/api/v1/appointments");

    private static async Task<string> ComputeBodyHashAsync(HttpContext ctx)
    {
        ctx.Request.Body.Position = 0;
        using var ms = new MemoryStream();
        await ctx.Request.Body.CopyToAsync(ms);
        var bytes = ms.ToArray();
        ctx.Request.Body.Position = 0;

        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
