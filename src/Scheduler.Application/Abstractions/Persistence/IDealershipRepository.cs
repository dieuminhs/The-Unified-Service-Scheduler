using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IDealershipRepository
{
    Task<Dealership?> GetByIdAsync(Guid id, CancellationToken ct);
}
