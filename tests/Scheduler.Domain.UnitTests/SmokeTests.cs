using FluentAssertions;
using NUnit.Framework;

namespace Scheduler.Domain.UnitTests;

[TestFixture]
public sealed class SmokeTests
{
    [Test]
    public void Runner_executes_at_least_one_test()
    {
        true.Should().BeTrue();
    }
}
