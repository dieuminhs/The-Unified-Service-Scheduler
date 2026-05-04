using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Idempotency;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Idempotency;

public sealed class IdempotencyStore : IIdempotencyStore
{
    private readonly SchedulerDbContext _ctx;

    public IdempotencyStore(SchedulerDbContext ctx) => _ctx = ctx;

    public async Task<IdempotencyHit?> TryGetAsync(string key, string bodyHash, CancellationToken ct)
    {
        var row = await _ctx.IdempotencyEntries.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key && e.BodyHash == bodyHash, ct);
        return row is null ? null : new IdempotencyHit(row.ResponseStatusCode, row.ResponseBody);
    }

    public async Task<bool> ExistsForDifferentBodyAsync(string key, string bodyHash, CancellationToken ct) =>
        await _ctx.IdempotencyEntries.AsNoTracking()
            .AnyAsync(e => e.Key == key && e.BodyHash != bodyHash, ct);

    public async Task PutAsync(string key, string bodyHash, string statusCode, string body, Guid? appointmentId,
        DateTime expiresAtUtc, CancellationToken ct)
    {
        _ctx.IdempotencyEntries.Add(new IdempotencyEntry
        {
            Key = key,
            BodyHash = bodyHash,
            ResponseStatusCode = statusCode,
            ResponseBody = body,
            AppointmentId = appointmentId,
            ExpiresAtUtc = expiresAtUtc
        });
        await _ctx.SaveChangesAsync(ct);
    }
}
