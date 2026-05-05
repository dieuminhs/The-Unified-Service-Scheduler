using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class CancellationServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

    private static Appointment Confirmed(DateTime start) =>
        Appointment.Confirm(
            dealershipId: Guid.NewGuid(),
            customerId: Guid.NewGuid(),
            vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(),
            technicianId: Guid.NewGuid(),
            serviceBayId: Guid.NewGuid(),
            startsAtUtc: start,
            endsAtUtc: start.AddHours(1));

    [Test]
    public async Task CancelAsync_ConfirmedAppointment_TransitionsToCancelled()
    {
        var appointment = Confirmed(Now.AddHours(1));
        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var uow = new Mock<IUnitOfWork>();
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Now);

        var sut = new CancellationService(writer.Object, uow.Object, clock.Object);
        var result = await sut.CancelAsync(appointment.Id, CancellationToken.None);

        result.Status.Should().Be(Domain.Enums.AppointmentStatus.Cancelled);
        uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void CancelAsync_UnknownId_ThrowsResourceNotFound()
    {
        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Appointment?)null);

        var sut = new CancellationService(writer.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IClock>());
        var act = async () => await sut.CancelAsync(Guid.NewGuid(), CancellationToken.None);
        act.Should().ThrowAsync<ResourceNotFoundException>();
    }

    [Test]
    public void CancelAsync_AlreadyCancelled_ThrowsAlreadyCancelled()
    {
        var appointment = Confirmed(Now.AddHours(1));
        appointment.Cancel(Now);

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(appointment.Id, It.IsAny<CancellationToken>())).ReturnsAsync(appointment);

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(Now);

        var sut = new CancellationService(writer.Object, Mock.Of<IUnitOfWork>(), clock.Object);
        var act = async () => await sut.CancelAsync(appointment.Id, CancellationToken.None);
        act.Should().ThrowAsync<AlreadyCancelledException>();
    }
}
