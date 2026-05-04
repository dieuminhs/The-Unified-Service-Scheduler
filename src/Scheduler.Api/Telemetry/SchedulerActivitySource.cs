using System.Diagnostics;

namespace Scheduler.Api.Telemetry;

public static class SchedulerActivitySource
{
    public const string Name = "Scheduler";
    public static readonly ActivitySource Instance = new(Name);
}
