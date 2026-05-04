using FluentAssertions;
using NUnit.Framework;
using Scheduler.Domain.Common;

namespace Scheduler.Domain.UnitTests.Common;

[TestFixture]
public sealed class TimeWindowTests
{
    private static DateTime At(int h, int m = 0) => new(2026, 5, 12, h, m, 0, DateTimeKind.Utc);

    [Test]
    public void Constructor_throws_when_end_not_after_start()
    {
        var act = () => new TimeWindow(At(10), At(10));
        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Adjacent_windows_do_not_overlap()
    {
        var a = new TimeWindow(At(10), At(11));
        var b = new TimeWindow(At(11), At(12));
        a.Overlaps(b).Should().BeFalse();
        b.Overlaps(a).Should().BeFalse();
    }

    [Test]
    public void Strict_overlap_detected()
    {
        var a = new TimeWindow(At(10), At(11));
        var b = new TimeWindow(At(10, 30), At(11, 30));
        a.Overlaps(b).Should().BeTrue();
    }

    [Test]
    public void Containment_is_overlap()
    {
        var outer = new TimeWindow(At(9), At(12));
        var inner = new TimeWindow(At(10), At(11));
        outer.Overlaps(inner).Should().BeTrue();
        inner.Overlaps(outer).Should().BeTrue();
    }

    [Test]
    public void Identical_windows_overlap()
    {
        var a = new TimeWindow(At(10), At(11));
        var b = new TimeWindow(At(10), At(11));
        a.Overlaps(b).Should().BeTrue();
    }
}
