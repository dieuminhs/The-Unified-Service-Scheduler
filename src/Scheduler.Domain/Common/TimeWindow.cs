namespace Scheduler.Domain.Common;

public readonly record struct TimeWindow
{
    public DateTime StartUtc { get; }
    public DateTime EndUtc { get; }

    public TimeWindow(DateTime startUtc, DateTime endUtc)
    {
        if (endUtc <= startUtc)
            throw new ArgumentException("End must be strictly after Start.", nameof(endUtc));

        StartUtc = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        EndUtc = DateTime.SpecifyKind(endUtc, DateTimeKind.Utc);
    }

    public TimeSpan Duration => EndUtc - StartUtc;

    public bool Overlaps(TimeWindow other)
        => StartUtc < other.EndUtc && other.StartUtc < EndUtc;

    public bool Contains(DateTime utc)
        => utc >= StartUtc && utc < EndUtc;
}
