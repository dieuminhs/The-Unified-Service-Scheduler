using Scheduler.Application.Contracts.Responses;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Mapping;

public static class AppointmentMappers
{
    public static AppointmentResponse ToResponse(this Appointment appointment) => new(
        Id: appointment.Id,
        DealershipId: appointment.DealershipId,
        CustomerId: appointment.CustomerId,
        VehicleId: appointment.VehicleId,
        ServiceTypeId: appointment.ServiceTypeId,
        TechnicianId: appointment.TechnicianId,
        ServiceBayId: appointment.ServiceBayId,
        StartsAtUtc: appointment.StartsAtUtc,
        EndsAtUtc: appointment.EndsAtUtc,
        Status: appointment.Status.ToString(),
        CreatedAtUtc: appointment.CreatedAtUtc);
}
