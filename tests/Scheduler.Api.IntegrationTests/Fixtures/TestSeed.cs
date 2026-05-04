using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Fixtures;

public sealed record SeedData(
    Dealership DealershipA,
    Dealership DealershipB,
    Customer Customer,
    Vehicle Vehicle,
    ServiceType QuickService,
    ServiceType EvService,
    Skill EvSkill,
    Skill DiagSkill,
    Technician AliceA,
    Technician BobAB,
    ServiceBay Bay1,
    ServiceBay Bay2);

public static class TestSeed
{
    public static async Task<SeedData> SeedAsync(SchedulerDbContext ctx)
    {
        var dA = new Dealership { Name = "A", TimeZone = "Etc/UTC", OpeningHours = AllWeek(0, 23) };
        var dB = new Dealership { Name = "B", TimeZone = "Etc/UTC", OpeningHours = AllWeek(0, 23) };

        var ev = new Skill { Code = "EV_CERT", Name = "EV" };
        var dg = new Skill { Code = "DIAG", Name = "Diag" };

        var quick = new ServiceType { Name = "Quick", DurationMinutes = 30 };
        var evSvc = new ServiceType { Name = "EV", DurationMinutes = 60,
            RequiredSkills = new List<ServiceTypeRequiredSkill> { new() { Skill = ev }, new() { Skill = dg } } };

        var alice = new Technician { FullName = "Alice",
            Skills = new List<TechnicianSkill> { new() { Skill = ev }, new() { Skill = dg } },
            DealershipAssignments = new List<TechnicianDealership> { new() { Dealership = dA } } };
        var bob = new Technician { FullName = "Bob",
            Skills = new List<TechnicianSkill> { new() { Skill = dg } },
            DealershipAssignments = new List<TechnicianDealership> { new() { Dealership = dA }, new() { Dealership = dB } } };

        var bay1 = new ServiceBay { Name = "Bay 1",
            DealershipAssignments = new List<ServiceBayDealership> { new() { Dealership = dA } } };
        var bay2 = new ServiceBay { Name = "Bay 2",
            DealershipAssignments = new List<ServiceBayDealership> { new() { Dealership = dA }, new() { Dealership = dB } } };

        var customer = new Customer { FirstName = "C", LastName = "X", Email = "c@x.example" };
        var vehicle = new Vehicle { CustomerId = customer.Id, Vin = "VIN1", Make = "Tesla", Model = "M3", Year = 2024 };

        ctx.AddRange(dA, dB, ev, dg, quick, evSvc, alice, bob, bay1, bay2, customer, vehicle);
        await ctx.SaveChangesAsync();

        return new SeedData(dA, dB, customer, vehicle, quick, evSvc, ev, dg, alice, bob, bay1, bay2);
    }

    private static List<OpeningHoursEntry> AllWeek(int openHour, int closeHour) =>
        Enum.GetValues<DayOfWeek>()
            .Select(d => new OpeningHoursEntry(d, new TimeOnly(openHour, 0), new TimeOnly(closeHour, 59)))
            .ToList();
}
