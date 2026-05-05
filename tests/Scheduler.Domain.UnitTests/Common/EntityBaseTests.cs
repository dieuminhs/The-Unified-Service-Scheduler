using FluentAssertions;
using NUnit.Framework;
using Scheduler.Domain.Common;

namespace Scheduler.Domain.UnitTests.Common;

[TestFixture]
public sealed class EntityBaseTests
{
    private sealed class FakeEntity : EntityBase { }

    [Test]
    public void Defaults_NewEntity_ActiveTrueDeletedFalse()
    {
        var e = new FakeEntity();
        e.IsActive.Should().BeTrue();
        e.IsDeleted.Should().BeFalse();
        e.DeletedAtUtc.Should().BeNull();
        e.UpdatedAtUtc.Should().BeNull();
    }

    [Test]
    public void Defaults_RowVersion_NotNull()
    {
        var e = new FakeEntity();
        e.RowVersion.Should().NotBeNull();
    }
}
