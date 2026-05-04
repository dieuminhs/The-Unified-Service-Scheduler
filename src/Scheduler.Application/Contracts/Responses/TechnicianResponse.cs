namespace Scheduler.Application.Contracts.Responses;

public sealed record TechnicianResponse(
    Guid Id,
    string FullName,
    IReadOnlyList<Guid> DealershipIds,
    IReadOnlyList<string> SkillCodes);
