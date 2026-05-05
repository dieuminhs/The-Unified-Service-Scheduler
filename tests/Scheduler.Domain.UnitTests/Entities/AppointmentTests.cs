using FluentAssertions;
using NUnit.Framework;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;

namespace Scheduler.Domain.UnitTests.Entities;

[TestFixture]
public sealed class AppointmentTests
{
    private static DateTime At(int h) => new(2026, 5, 12, h, 0, 0, DateTimeKind.Utc);

    [Test]
    public void Confirm_EndNotAfterStart_Throws()
    {
        var act = () => Appointment.Confirm(
            dealershipId: Guid.NewGuid(),
            customerId:   Guid.NewGuid(),
            vehicleId:    Guid.NewGuid(),
            serviceTypeId:Guid.NewGuid(),
            technicianId: Guid.NewGuid(),
            serviceBayId: Guid.NewGuid(),
            startsAtUtc:  At(10),
            endsAtUtc:    At(10));

        act.Should().Throw<ArgumentException>();
    }

    [Test]
    public void Confirm_ValidArgs_StatusConfirmed()
    {
        var a = Appointment.Confirm(
            dealershipId: Guid.NewGuid(), customerId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(), technicianId: Guid.NewGuid(), serviceBayId: Guid.NewGuid(),
            startsAtUtc: At(9), endsAtUtc: At(10));

        a.Status.Should().Be(AppointmentStatus.Confirmed);
        a.Id.Should().NotBe(Guid.Empty);
    }

    [Test]
    public void Cancel_Confirmed_TransitionsToCancelledWithTimestamp()
    {
        var a = Appointment.Confirm(
            dealershipId: Guid.NewGuid(), customerId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(), technicianId: Guid.NewGuid(), serviceBayId: Guid.NewGuid(),
            startsAtUtc: At(9), endsAtUtc: At(10));

        var ts = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        a.Cancel(ts);

        a.Status.Should().Be(AppointmentStatus.Cancelled);
        a.CancelledAtUtc.Should().Be(ts);
    }

    [Test]
    public void Cancel_AlreadyCancelled_Throws()
    {
        var a = Appointment.Confirm(
            dealershipId: Guid.NewGuid(), customerId: Guid.NewGuid(), vehicleId: Guid.NewGuid(),
            serviceTypeId: Guid.NewGuid(), technicianId: Guid.NewGuid(), serviceBayId: Guid.NewGuid(),
            startsAtUtc: At(9), endsAtUtc: At(10));

        a.Cancel(DateTime.UtcNow);
        var act = () => a.Cancel(DateTime.UtcNow);

        act.Should().Throw<InvalidOperationException>();
    }
}
