namespace Scheduler.Application.Contracts.Responses;

public sealed record ServiceBayResponse(Guid Id, string Name, IReadOnlyList<Guid> DealershipIds);
