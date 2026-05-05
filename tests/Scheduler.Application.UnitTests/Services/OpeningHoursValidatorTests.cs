using FluentAssertions;
using NUnit.Framework;
using Scheduler.Application.Services;
using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class OpeningHoursValidatorTests
{
    private static Dealership Etcd(params OpeningHoursEntry[] hours) => new()
    {
        Name = "Test",
        TimeZone = "Etc/UTC",
        OpeningHours = new(hours)
    };

    [Test]
    public void IsWithinOpeningHours_WithinHours_ReturnsTrue()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 9, 30, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);

        sut.IsWithinOpeningHours(d, start, end).Should().BeTrue();
    }

    [Test]
    public void IsWithinOpeningHours_EndAtClose_ReturnsTrue()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 16, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 5, 12, 17, 0, 0, DateTimeKind.Utc);

        sut.IsWithinOpeningHours(d, start, end).Should().BeTrue();
    }

    [Test]
    public void IsWithinOpeningHours_StartBeforeOpening_ReturnsFalse()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 8, 30, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);

        sut.IsWithinOpeningHours(d, start, end).Should().BeFalse();
    }

    [Test]
    public void IsWithinOpeningHours_EndAfterClosing_ReturnsFalse()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Tuesday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 16, 30, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 5, 12, 17, 30, 0, DateTimeKind.Utc);

        sut.IsWithinOpeningHours(d, start, end).Should().BeFalse();
    }

    [Test]
    public void IsWithinOpeningHours_NoHoursForDay_ReturnsFalse()
    {
        var d = Etcd(new OpeningHoursEntry(DayOfWeek.Monday, new(9, 0), new(17, 0)));
        var sut = new OpeningHoursValidator();
        var start = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc);
        var end = start.AddHours(1);

        sut.IsWithinOpeningHours(d, start, end).Should().BeFalse();
    }
}
