using FluentAssertions;
using NetArchTest.Rules;
using NUnit.Framework;

namespace Scheduler.ArchitectureTests;

[TestFixture]
public sealed class LayerDependencyTests
{
    [Test]
    public void Domain_DoesNotDependOnAnythingExternal()
    {
        var result = Types.InAssembly(typeof(Scheduler.Domain.Common.EntityBase).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .And().NotHaveDependencyOn("Microsoft.AspNetCore")
            .And().NotHaveDependencyOn("Scheduler.Application")
            .And().NotHaveDependencyOn("Scheduler.Infrastructure")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames is null ? "" : string.Join(", ", result.FailingTypeNames));
    }

    [Test]
    public void Application_DoesNotDependOnEFOrAspNetCore()
    {
        var result = Types.InAssembly(typeof(Scheduler.Application.Services.BookingService).Assembly)
            .Should()
            .NotHaveDependencyOn("Microsoft.EntityFrameworkCore")
            .And().NotHaveDependencyOn("Microsoft.AspNetCore")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(result.FailingTypeNames is null ? "" : string.Join(", ", result.FailingTypeNames));
    }
}
