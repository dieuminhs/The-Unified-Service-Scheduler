namespace Scheduler.Application.Contracts.Responses;

public sealed record AvailabilitySlotResponse(DateTime StartsAtUtc, DateTime EndsAtUtc);
