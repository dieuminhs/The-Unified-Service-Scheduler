using Scheduler.Domain.Common;

namespace Scheduler.Infrastructure.Persistence;

public sealed class IdempotencyEntry : EntityBase
{
    public string Key { get; set; } = string.Empty;
    public string BodyHash { get; set; } = string.Empty;
    public Guid? AppointmentId { get; set; }
    public string ResponseStatusCode { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
}
