using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class ServiceTypeRepository : IServiceTypeRepository
{
    private readonly SchedulerDbContext _ctx;
    public ServiceTypeRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<ServiceType?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.ServiceTypes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && s.IsActive, ct);
}
