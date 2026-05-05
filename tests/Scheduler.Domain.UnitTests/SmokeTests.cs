using FluentAssertions;
using NUnit.Framework;

namespace Scheduler.Domain.UnitTests;

[TestFixture]
public sealed class SmokeTests
{
    [Test]
    public void Runner_ExecutesAtLeastOneTest()
    {
        true.Should().BeTrue();
    }
}
