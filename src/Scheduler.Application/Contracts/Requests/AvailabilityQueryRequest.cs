namespace Scheduler.Application.Contracts.Requests;

public sealed record AvailabilityQueryRequest(
    Guid DealershipId,
    Guid ServiceTypeId,
    DateTime FromUtc,
    DateTime ToUtc,
    int GranularityMinutes);
