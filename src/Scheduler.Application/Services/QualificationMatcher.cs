using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services;

public sealed class QualificationMatcher : IQualificationMatcher
{
    private readonly ITechnicianRepository _repo;

    public QualificationMatcher(ITechnicianRepository repo)
    {
        _repo = repo;
    }

    public Task<IReadOnlyList<Technician>> FindQualifiedAsync(
        Guid serviceTypeId,
        Guid dealershipId,
        CancellationToken ct) =>
        _repo.ListQualifiedForServiceAtDealershipAsync(serviceTypeId, dealershipId, ct);
}
