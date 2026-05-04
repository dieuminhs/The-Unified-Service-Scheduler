namespace Scheduler.Application.Abstractions.Idempotency;

public sealed record IdempotencyHit(string ResponseStatusCode, string ResponseBody);

public interface IIdempotencyStore
{
    Task<IdempotencyHit?> TryGetAsync(string key, string bodyHash, CancellationToken ct);
    Task PutAsync(string key, string bodyHash, string statusCode, string body, Guid? appointmentId,
        DateTime expiresAtUtc, CancellationToken ct);
    Task<bool> ExistsForDifferentBodyAsync(string key, string bodyHash, CancellationToken ct);
}
