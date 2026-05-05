using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class ReferenceDataController : ControllerBase
{
    private readonly SchedulerDbContext _ctx;
    public ReferenceDataController(SchedulerDbContext ctx) => _ctx = ctx;

    [HttpGet("dealerships")]
    public Task<List<object>> ListDealerships(CancellationToken ct) =>
        _ctx.Dealerships.AsNoTracking()
            .Select(d => (object)new { d.Id, d.Name, d.TimeZone, d.OpeningHours })
            .ToListAsync(ct);

    [HttpGet("dealerships/{id:guid}")]
    public async Task<IActionResult> GetDealership(Guid id, CancellationToken ct)
    {
        var d = await _ctx.Dealerships.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return d is null ? NotFound() : Ok(new { d.Id, d.Name, d.TimeZone, d.OpeningHours });
    }

    [HttpGet("dealerships/{id:guid}/technicians")]
    public Task<List<object>> ListTechniciansAtDealership(Guid id, CancellationToken ct) =>
        _ctx.TechnicianDealerships.AsNoTracking()
            .Where(td => td.DealershipId == id)
            .Select(td => td.Technician!)
            .Select(t => (object)new { t.Id, t.FullName })
            .ToListAsync(ct);

    [HttpGet("dealerships/{id:guid}/bays")]
    public Task<List<object>> ListBaysAtDealership(Guid id, CancellationToken ct) =>
        _ctx.ServiceBayDealerships.AsNoTracking()
            .Where(sbd => sbd.DealershipId == id)
            .Select(sbd => sbd.ServiceBay!)
            .Select(b => (object)new { b.Id, b.Name })
            .ToListAsync(ct);

    [HttpGet("technicians")]
    public Task<List<object>> ListTechnicians(CancellationToken ct) =>
        _ctx.Technicians.AsNoTracking()
            .Select(t => (object)new
            {
                t.Id,
                t.FullName,
                DealershipIds = t.DealershipAssignments.Select(td => td.DealershipId),
                Skills = t.Skills.Select(ts => ts.Skill!.Code)
            })
            .ToListAsync(ct);

    [HttpGet("bays")]
    public Task<List<object>> ListBays(CancellationToken ct) =>
        _ctx.ServiceBays.AsNoTracking()
            .Select(b => (object)new
            {
                b.Id,
                b.Name,
                DealershipIds = b.DealershipAssignments.Select(sbd => sbd.DealershipId)
            })
            .ToListAsync(ct);

    [HttpGet("service-types")]
    public Task<List<object>> ListServiceTypes(CancellationToken ct) =>
        _ctx.ServiceTypes.AsNoTracking()
            .Select(s => (object)new
            {
                s.Id, s.Name, s.DurationMinutes, s.Description,
                RequiredSkills = s.RequiredSkills.Select(r => r.Skill!.Code)
            })
            .ToListAsync(ct);

    [HttpGet("skills")]
    public Task<List<object>> ListSkills(CancellationToken ct) =>
        _ctx.Skills.AsNoTracking()
            .Select(s => (object)new { s.Id, s.Code, s.Name, s.Description, s.Category })
            .ToListAsync(ct);

    [HttpGet("customers/{id:guid}/vehicles")]
    public Task<List<object>> ListVehiclesForCustomer(Guid id, CancellationToken ct) =>
        _ctx.Vehicles.AsNoTracking()
            .Where(v => v.CustomerId == id)
            .Select(v => (object)new { v.Id, v.Vin, v.Make, v.Model, v.Year })
            .ToListAsync(ct);

    [HttpGet("customers")]
    public Task<List<object>> ListCustomers(CancellationToken ct) =>
        _ctx.Customers.AsNoTracking()
            .Select(c => (object)new
            {
                c.Id,
                c.FirstName,
                c.LastName,
                c.Email,
                Vehicles = c.Vehicles.Select(v => new { v.Id, v.Vin, v.Make, v.Model, v.Year })
            })
            .ToListAsync(ct);
}
