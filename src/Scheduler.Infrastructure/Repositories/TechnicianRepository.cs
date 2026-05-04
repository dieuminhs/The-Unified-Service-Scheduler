using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class TechnicianRepository : ITechnicianRepository
{
    private readonly SchedulerDbContext _ctx;
    public TechnicianRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<Technician>> ListQualifiedForServiceAtDealershipAsync(
        Guid serviceTypeId, Guid dealershipId, CancellationToken ct)
    {
        var requiredSkillIds = await _ctx.ServiceTypeRequiredSkills
            .Where(r => r.ServiceTypeId == serviceTypeId)
            .Select(r => r.SkillId)
            .ToListAsync(ct);

        var query = _ctx.Technicians
            .AsNoTracking()
            .Where(t => t.IsActive)
            .Where(t => _ctx.TechnicianDealerships.Any(td => td.TechnicianId == t.Id && td.DealershipId == dealershipId));

        foreach (var skillId in requiredSkillIds)
        {
            var sid = skillId;
            query = query.Where(t => _ctx.TechnicianSkills.Any(ts => ts.TechnicianId == t.Id && ts.SkillId == sid));
        }

        return await query.OrderBy(t => t.Id).ToListAsync(ct);
    }
}
