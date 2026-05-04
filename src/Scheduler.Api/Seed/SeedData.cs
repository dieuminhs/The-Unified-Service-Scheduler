using Microsoft.EntityFrameworkCore;
using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.Seed;

public static class SeedData
{
    public static async Task SeedAsync(SchedulerDbContext ctx, CancellationToken ct = default)
    {
        if (await ctx.Dealerships.AnyAsync(ct)) return;

        var dealershipA = new Dealership
        {
            Name = "Keyloop Auto — Central",
            TimeZone = "Etc/UTC",
            OpeningHours = new List<OpeningHoursEntry>
            {
                new(DayOfWeek.Monday,    new(9, 0), new(17, 0)),
                new(DayOfWeek.Tuesday,   new(9, 0), new(17, 0)),
                new(DayOfWeek.Wednesday, new(9, 0), new(17, 0)),
                new(DayOfWeek.Thursday,  new(9, 0), new(17, 0)),
                new(DayOfWeek.Friday,    new(9, 0), new(17, 0)),
            }
        };

        var dealershipB = new Dealership
        {
            Name = "Keyloop Auto — North",
            TimeZone = "Etc/UTC",
            OpeningHours = new List<OpeningHoursEntry>
            {
                new(DayOfWeek.Monday,    new(8, 0), new(18, 0)),
                new(DayOfWeek.Tuesday,   new(8, 0), new(18, 0)),
                new(DayOfWeek.Wednesday, new(8, 0), new(18, 0)),
                new(DayOfWeek.Thursday,  new(8, 0), new(18, 0)),
                new(DayOfWeek.Friday,    new(8, 0), new(18, 0)),
                new(DayOfWeek.Saturday,  new(9, 0), new(13, 0)),
            }
        };

        var skillEv  = new Skill { Code = "EV_CERT", Name = "EV Certification", Category = "Powertrain" };
        var skillBrk = new Skill { Code = "BRAKES",  Name = "Brake Systems",     Category = "Chassis" };
        var skillDg  = new Skill { Code = "DIAG",    Name = "Diagnostics",       Category = "General" };

        var oilChange = new ServiceType { Name = "Oil Change", DurationMinutes = 30 };
        var brakeJob  = new ServiceType
        {
            Name = "Brake Replacement",
            DurationMinutes = 90,
            RequiredSkills = new List<ServiceTypeRequiredSkill>
            {
                new() { Skill = skillBrk }
            }
        };
        var evService = new ServiceType
        {
            Name = "EV Diagnostic Service",
            DurationMinutes = 60,
            RequiredSkills = new List<ServiceTypeRequiredSkill>
            {
                new() { Skill = skillEv },
                new() { Skill = skillDg }
            }
        };

        var techAlice = new Technician
        {
            FullName = "Alice Smith",
            Skills = new List<TechnicianSkill>
            {
                new() { Skill = skillEv },
                new() { Skill = skillDg }
            }
        };
        var techBob = new Technician
        {
            FullName = "Bob Jones",
            Skills = new List<TechnicianSkill>
            {
                new() { Skill = skillBrk },
                new() { Skill = skillDg }
            }
        };
        var techCara = new Technician
        {
            FullName = "Cara Liu",
            Skills = new List<TechnicianSkill>
            {
                new() { Skill = skillEv },
                new() { Skill = skillBrk },
                new() { Skill = skillDg }
            }
        };

        var bay1 = new ServiceBay { Name = "Bay 1" };
        var bay2 = new ServiceBay { Name = "Bay 2" };
        var bay3 = new ServiceBay { Name = "Bay 3" };

        // Assignments
        techAlice.DealershipAssignments = new List<TechnicianDealership>
        {
            new() { Dealership = dealershipA }
        };
        techBob.DealershipAssignments = new List<TechnicianDealership>
        {
            new() { Dealership = dealershipA }
        };
        techCara.DealershipAssignments = new List<TechnicianDealership>
        {
            new() { Dealership = dealershipA },
            new() { Dealership = dealershipB }
        };
        bay1.DealershipAssignments = new List<ServiceBayDealership>
        {
            new() { Dealership = dealershipA }
        };
        bay2.DealershipAssignments = new List<ServiceBayDealership>
        {
            new() { Dealership = dealershipA }
        };
        bay3.DealershipAssignments = new List<ServiceBayDealership>
        {
            new() { Dealership = dealershipB }
        };

        var customer = new Customer { FirstName = "Casey", LastName = "Garcia", Email = "casey@example.com", Phone = "+15551234567" };
        var vehicle  = new Vehicle { CustomerId = customer.Id, Vin = "1HGCM82633A004352", Make = "Tesla", Model = "Model 3", Year = 2024 };

        ctx.AddRange(dealershipA, dealershipB,
            skillEv, skillBrk, skillDg,
            oilChange, brakeJob, evService,
            techAlice, techBob, techCara,
            bay1, bay2, bay3,
            customer, vehicle);

        await ctx.SaveChangesAsync(ct);
    }
}
