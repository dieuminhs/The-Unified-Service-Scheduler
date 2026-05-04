namespace Scheduler.Application.Contracts.Requests;

public sealed record RescheduleAppointmentRequest(DateTime NewStartsAtUtc);
