using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class AvailabilityServiceTests
{
    private static DateTime At(int h) => new(2026, 5, 12, h, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task FirstFreeTechnician_NoOverlap_ReturnsFirst()
    {
        var t1 = new Technician { FullName = "A" };
        var t2 = new Technician { FullName = "B" };

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.AnyTechnicianOverlapAsync(t1.Id, At(9), At(10), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writer.Setup(w => w.AnyTechnicianOverlapAsync(t2.Id, At(9), At(10), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new AvailabilityService(
            writer.Object,
            Mock.Of<IDealershipRepository>(),
            Mock.Of<IServiceTypeRepository>(),
            Mock.Of<IServiceBayRepository>(),
            Mock.Of<ITechnicianRepository>());

        var picked = await sut.FirstFreeTechnicianAsync(new[] { t1, t2 }, At(9), At(10), CancellationToken.None);

        picked.Should().Be(t2);
    }

    [Test]
    public async Task FirstFreeTechnician_AllBusy_ReturnsNull()
    {
        var t1 = new Technician { FullName = "A" };

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.AnyTechnicianOverlapAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var sut = new AvailabilityService(
            writer.Object,
            Mock.Of<IDealershipRepository>(),
            Mock.Of<IServiceTypeRepository>(),
            Mock.Of<IServiceBayRepository>(),
            Mock.Of<ITechnicianRepository>());

        var picked = await sut.FirstFreeTechnicianAsync(new[] { t1 }, At(9), At(10), CancellationToken.None);

        picked.Should().BeNull();
    }

    [Test]
    public async Task FirstFreeBay_NoOverlap_ReturnsFirst()
    {
        var b1 = new ServiceBay { Name = "Bay 1" };
        var b2 = new ServiceBay { Name = "Bay 2" };

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.AnyBayOverlapAsync(b1.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        writer.Setup(w => w.AnyBayOverlapAsync(b2.Id, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var sut = new AvailabilityService(
            writer.Object,
            Mock.Of<IDealershipRepository>(),
            Mock.Of<IServiceTypeRepository>(),
            Mock.Of<IServiceBayRepository>(),
            Mock.Of<ITechnicianRepository>());

        var picked = await sut.FirstFreeBayAsync(new[] { b1, b2 }, At(9), At(10), CancellationToken.None);

        picked.Should().Be(b2);
    }
}
