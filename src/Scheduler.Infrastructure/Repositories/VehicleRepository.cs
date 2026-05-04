using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly SchedulerDbContext _ctx;
    public VehicleRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id && v.IsActive, ct);
}
