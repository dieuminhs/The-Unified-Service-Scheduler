using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface ITechnicianRepository
{
    Task<IReadOnlyList<Technician>> ListQualifiedForServiceAtDealershipAsync(
        Guid serviceTypeId, Guid dealershipId, CancellationToken ct);
}
