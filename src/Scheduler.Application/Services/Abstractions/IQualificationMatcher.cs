using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IQualificationMatcher
{
    Task<IReadOnlyList<Technician>> FindQualifiedAsync(Guid serviceTypeId, Guid dealershipId, CancellationToken ct);
}
