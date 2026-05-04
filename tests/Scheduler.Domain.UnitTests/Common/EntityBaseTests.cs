using FluentAssertions;
using NUnit.Framework;
using Scheduler.Domain.Common;

namespace Scheduler.Domain.UnitTests.Common;

[TestFixture]
public sealed class EntityBaseTests
{
    private sealed class FakeEntity : EntityBase { }

    [Test]
    public void New_entity_has_active_true_and_deleted_false()
    {
        var e = new FakeEntity();
        e.IsActive.Should().BeTrue();
        e.IsDeleted.Should().BeFalse();
        e.DeletedAtUtc.Should().BeNull();
        e.UpdatedAtUtc.Should().BeNull();
    }

    [Test]
    public void RowVersion_initialised_as_empty_array_not_null()
    {
        var e = new FakeEntity();
        e.RowVersion.Should().NotBeNull();
    }
}
