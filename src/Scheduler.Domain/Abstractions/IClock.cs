namespace Scheduler.Domain.Abstractions;

public interface IClock
{
    DateTime UtcNow { get; }
}
