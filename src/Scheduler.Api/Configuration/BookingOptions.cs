namespace Scheduler.Api.Configuration;

public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    public int MaxRetries { get; set; } = 3;
    public int RetryBaseDelayMs { get; set; } = 50;
    public int DefaultGranularityMinutes { get; set; } = 15;
}
