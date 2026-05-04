namespace Scheduler.Domain.ValueObjects;

public sealed record OpeningHoursEntry(
    DayOfWeek DayOfWeek,
    TimeOnly OpensAt,
    TimeOnly ClosesAt)
{
    public bool Contains(TimeOnly local)
        => local >= OpensAt && local < ClosesAt;
}
