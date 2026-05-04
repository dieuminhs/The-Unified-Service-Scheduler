namespace Scheduler.Api.Configuration;

public sealed class SeedingOptions
{
    public const string SectionName = "Seeding";

    public bool Enabled { get; set; } = true;
}
