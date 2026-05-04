using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class DealershipRepository : IDealershipRepository
{
    private readonly SchedulerDbContext _ctx;
    public DealershipRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Dealership?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Dealerships.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id && d.IsActive, ct);
}
