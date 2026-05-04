using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Enums;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class AppointmentRepository : IAppointmentReader, IAppointmentWriter
{
    private readonly SchedulerDbContext _ctx;

    public AppointmentRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Appointments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Appointment>> ListAsync(
        Guid? dealershipId, Guid? customerId, Guid? technicianId,
        DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken ct)
    {
        var q = _ctx.Appointments.AsNoTracking().AsQueryable();
        if (dealershipId is not null) q = q.Where(a => a.DealershipId == dealershipId);
        if (customerId is not null) q = q.Where(a => a.CustomerId == customerId);
        if (technicianId is not null) q = q.Where(a => a.TechnicianId == technicianId);
        if (fromUtc is not null) q = q.Where(a => a.StartsAtUtc >= fromUtc);
        if (toUtc is not null) q = q.Where(a => a.StartsAtUtc <= toUtc);
        return await q.OrderBy(a => a.StartsAtUtc).Skip(skip).Take(take).ToListAsync(ct);
    }

    public async Task AddAsync(Appointment appointment, CancellationToken ct) =>
        await _ctx.Appointments.AddAsync(appointment, ct);

    public Task<bool> AnyTechnicianOverlapAsync(Guid technicianId, DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
        _ctx.Appointments.AnyAsync(a =>
            a.TechnicianId == technicianId &&
            a.Status == AppointmentStatus.Confirmed &&
            a.StartsAtUtc < endUtc && startUtc < a.EndsAtUtc, ct);

    public Task<bool> AnyBayOverlapAsync(Guid bayId, DateTime startUtc, DateTime endUtc, CancellationToken ct) =>
        _ctx.Appointments.AnyAsync(a =>
            a.ServiceBayId == bayId &&
            a.Status == AppointmentStatus.Confirmed &&
            a.StartsAtUtc < endUtc && startUtc < a.EndsAtUtc, ct);

    public Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        _ctx.Appointments.FirstOrDefaultAsync(a => a.Id == id, ct);
}
