using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IServiceTypeRepository
{
    Task<ServiceType?> GetByIdAsync(Guid id, CancellationToken ct);
}
