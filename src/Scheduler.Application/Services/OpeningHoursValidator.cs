using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services;

public sealed class OpeningHoursValidator : IOpeningHoursValidator
{
    public bool IsWithinOpeningHours(Dealership dealership, DateTime startUtc, DateTime endUtc)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(dealership.TimeZone);
        var localStart = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), tz);
        var localEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(endUtc, DateTimeKind.Utc), tz);

        if (localStart.Date != localEnd.Date)
        {
            return false; // v1 does not support cross-day bookings.
        }

        var entry = dealership.OpeningHours.FirstOrDefault(hours => hours.DayOfWeek == localStart.DayOfWeek);
        if (entry is null)
        {
            return false;
        }

        var startTime = TimeOnly.FromDateTime(localStart);
        var endTime = TimeOnly.FromDateTime(localEnd);
        return startTime >= entry.OpensAt && endTime <= entry.ClosesAt;
    }
}
