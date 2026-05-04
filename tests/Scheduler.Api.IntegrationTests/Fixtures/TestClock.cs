using Scheduler.Domain.Abstractions;

namespace Scheduler.Api.IntegrationTests.Fixtures;

public sealed class TestClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc);
}
