using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class QualificationMatcherTests
{
    [Test]
    public async Task Delegates_to_repository_and_returns_result()
    {
        var serviceTypeId = Guid.NewGuid();
        var dealershipId = Guid.NewGuid();
        var technicians = new List<Technician> { new() { FullName = "Bob" } };

        var repo = new Mock<ITechnicianRepository>();
        repo.Setup(r => r.ListQualifiedForServiceAtDealershipAsync(serviceTypeId, dealershipId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(technicians);

        var sut = new QualificationMatcher(repo.Object);
        var result = await sut.FindQualifiedAsync(serviceTypeId, dealershipId, CancellationToken.None);

        result.Should().BeEquivalentTo(technicians);
    }
}
