using Scheduler.Domain.Common;
using Scheduler.Domain.Enums;

namespace Scheduler.Domain.Entities;

public sealed class Appointment : EntityBase
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DealershipId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid VehicleId { get; private set; }
    public Guid ServiceTypeId { get; private set; }
    public Guid TechnicianId { get; private set; }
    public Guid ServiceBayId { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime EndsAtUtc { get; private set; }
    public AppointmentStatus Status { get; private set; }
    public DateTime? CancelledAtUtc { get; private set; }

    private Appointment() { }

    public static Appointment Confirm(
        Guid dealershipId,
        Guid customerId,
        Guid vehicleId,
        Guid serviceTypeId,
        Guid technicianId,
        Guid serviceBayId,
        DateTime startsAtUtc,
        DateTime endsAtUtc)
    {
        if (endsAtUtc <= startsAtUtc)
            throw new ArgumentException("End must be strictly after Start.", nameof(endsAtUtc));

        if (dealershipId == Guid.Empty) throw new ArgumentException("Required.", nameof(dealershipId));
        if (customerId == Guid.Empty)   throw new ArgumentException("Required.", nameof(customerId));
        if (vehicleId == Guid.Empty)    throw new ArgumentException("Required.", nameof(vehicleId));
        if (serviceTypeId == Guid.Empty)throw new ArgumentException("Required.", nameof(serviceTypeId));
        if (technicianId == Guid.Empty) throw new ArgumentException("Required.", nameof(technicianId));
        if (serviceBayId == Guid.Empty) throw new ArgumentException("Required.", nameof(serviceBayId));

        return new Appointment
        {
            DealershipId = dealershipId,
            CustomerId = customerId,
            VehicleId = vehicleId,
            ServiceTypeId = serviceTypeId,
            TechnicianId = technicianId,
            ServiceBayId = serviceBayId,
            StartsAtUtc = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc),
            EndsAtUtc   = DateTime.SpecifyKind(endsAtUtc,   DateTimeKind.Utc),
            Status = AppointmentStatus.Confirmed
        };
    }

    public void Cancel(DateTime utcNow)
    {
        if (Status == AppointmentStatus.Cancelled)
            throw new InvalidOperationException("Appointment is already cancelled.");

        Status = AppointmentStatus.Cancelled;
        CancelledAtUtc = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
    }
}
