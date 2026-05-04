using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
}
