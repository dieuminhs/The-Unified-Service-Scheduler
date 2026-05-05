using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduler.Domain.Enums;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1/report")]
public sealed class ReportController : ControllerBase
{
    private readonly SchedulerDbContext _ctx;
    public ReportController(SchedulerDbContext ctx) => _ctx = ctx;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var total = await _ctx.Appointments.CountAsync(ct);

        var byStatusRows = await _ctx.Appointments
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var byStatus = byStatusRows.ToDictionary(x => x.Status.ToString(), x => x.Count);

        var byDealership = await _ctx.Appointments
            .Join(_ctx.Dealerships, a => a.DealershipId, d => d.Id, (a, d) => new { a, d })
            .GroupBy(x => new { x.d.Id, x.d.Name })
            .Select(g => new
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                Total = g.Count(),
                Confirmed = g.Count(x => x.a.Status == AppointmentStatus.Confirmed),
                Cancelled = g.Count(x => x.a.Status == AppointmentStatus.Cancelled)
            })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        var byServiceType = await _ctx.Appointments
            .Join(_ctx.ServiceTypes, a => a.ServiceTypeId, s => s.Id, (a, s) => new { a, s })
            .GroupBy(x => new { x.s.Id, x.s.Name })
            .Select(g => new { Id = g.Key.Id, Name = g.Key.Name, Total = g.Count() })
            .OrderByDescending(x => x.Total)
            .ToListAsync(ct);

        return Ok(new
        {
            TotalAppointments = total,
            ByStatus = byStatus,
            ByDealership = byDealership,
            ByServiceType = byServiceType,
            GeneratedAtUtc = DateTime.UtcNow
        });
    }
}
