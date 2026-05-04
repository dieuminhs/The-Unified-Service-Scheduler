namespace Scheduler.Application.Contracts.Responses;

public sealed record AppointmentResponse(
    Guid Id,
    Guid DealershipId,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    Guid TechnicianId,
    Guid ServiceBayId,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Status,
    DateTime CreatedAtUtc);
