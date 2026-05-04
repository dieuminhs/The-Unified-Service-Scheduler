namespace Scheduler.Application.Contracts.Requests;

public sealed record BookAppointmentRequest(
    Guid DealershipId,
    Guid CustomerId,
    Guid VehicleId,
    Guid ServiceTypeId,
    DateTime StartsAtUtc);
