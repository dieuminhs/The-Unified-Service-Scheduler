using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class ServiceBayRepository : IServiceBayRepository
{
    private readonly SchedulerDbContext _ctx;
    public ServiceBayRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<ServiceBay>> ListAtDealershipAsync(Guid dealershipId, CancellationToken ct)
    {
        return await _ctx.ServiceBays.AsNoTracking()
            .Where(b => b.IsActive)
            .Where(b => _ctx.ServiceBayDealerships.Any(sbd => sbd.ServiceBayId == b.Id && sbd.DealershipId == dealershipId))
            .OrderBy(b => b.Id)
            .ToListAsync(ct);
    }
}
