using Scheduler.Domain.Abstractions;

namespace Scheduler.Infrastructure.Time;

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
