using FluentAssertions;
using NUnit.Framework;
using Scheduler.Application.Services;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class ResourceSelectorTests
{
    [Test]
    public void Pick_NonEmptyList_ReturnsFirst()
    {
        var sut = new ResourceSelector();
        var picked = sut.Pick(new List<string> { "a", "b", "c" });
        picked.Should().Be("a");
    }

    [Test]
    public void Pick_EmptyList_ReturnsNull()
    {
        var sut = new ResourceSelector();
        var picked = sut.Pick(new List<string>());
        picked.Should().BeNull();
    }
}
