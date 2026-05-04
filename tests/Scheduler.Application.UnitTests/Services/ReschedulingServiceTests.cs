using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class ReschedulingServiceTests
{
    [Test]
    public async Task Cancels_old_and_books_new_atomically()
    {
        var now = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        var dealershipId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var serviceTypeId = Guid.NewGuid();
        var existing = Appointment.Confirm(
            dealershipId,
            customerId,
            vehicleId,
            serviceTypeId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            now.AddHours(2),
            now.AddHours(3));

        var newStart = now.AddHours(5);
        var newAppointment = Appointment.Confirm(
            dealershipId,
            customerId,
            vehicleId,
            serviceTypeId,
            Guid.NewGuid(),
            Guid.NewGuid(),
            newStart,
            newStart.AddHours(1));

        var writer = new Mock<IAppointmentWriter>();
        writer.Setup(w => w.GetForUpdateAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.BeginSerializableTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Mock.Of<IAsyncDisposable>());

        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(now);

        var booking = new Mock<IBookingService>();
        booking.Setup(b => b.BookAsync(It.IsAny<BookAppointmentRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(newAppointment);

        var sut = new ReschedulingService(writer.Object, uow.Object, clock.Object, booking.Object);

        var result = await sut.RescheduleAsync(existing.Id, new RescheduleAppointmentRequest(newStart), CancellationToken.None);

        result.Should().Be(newAppointment);
        existing.Status.Should().Be(Domain.Enums.AppointmentStatus.Cancelled);
        uow.Verify(u => u.BeginSerializableTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
