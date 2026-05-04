using System.Diagnostics.Metrics;

namespace Scheduler.Api.Telemetry;

public sealed class SchedulerMetrics
{
    public const string MeterName = "Scheduler.Bookings";

    public Counter<long> BookingsTotal { get; }
    public Counter<long> BookingRetriesTotal { get; }
    public Histogram<double> BookingDurationSeconds { get; }
    public Histogram<double> AvailabilityQueryDurationSeconds { get; }
    public Counter<long> IdempotencyReplaysTotal { get; }

    public SchedulerMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        BookingsTotal = meter.CreateCounter<long>("bookings_total");
        BookingRetriesTotal = meter.CreateCounter<long>("booking_retries_total");
        BookingDurationSeconds = meter.CreateHistogram<double>("booking_duration_seconds", unit: "s");
        AvailabilityQueryDurationSeconds = meter.CreateHistogram<double>("availability_query_duration_seconds", unit: "s");
        IdempotencyReplaysTotal = meter.CreateCounter<long>("idempotency_replays_total");
    }
}
