using FluentAssertions;
using NetArchTest.Rules;
using NUnit.Framework;
using Scheduler.Domain.Common;

namespace Scheduler.ArchitectureTests;

[TestFixture]
public sealed class EntityBaseArchTests
{
    [Test]
    public void AllEntities_InheritEntityBase()
    {
        var result = Types.InAssembly(typeof(EntityBase).Assembly)
            .That().ResideInNamespace("Scheduler.Domain.Entities")
            .Should().Inherit(typeof(EntityBase))
            .GetResult();

        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames is null ? "" : string.Join(", ", result.FailingTypeNames));
    }
}
