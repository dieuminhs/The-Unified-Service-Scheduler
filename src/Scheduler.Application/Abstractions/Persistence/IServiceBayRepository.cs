using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IServiceBayRepository
{
    Task<IReadOnlyList<ServiceBay>> ListAtDealershipAsync(Guid dealershipId, CancellationToken ct);
}
