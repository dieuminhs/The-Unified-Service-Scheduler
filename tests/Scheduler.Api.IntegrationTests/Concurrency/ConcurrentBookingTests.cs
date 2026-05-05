using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Scheduler.Api.IntegrationTests.Fixtures;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Domain.Enums;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Concurrency;

[TestFixture]
[Category("Concurrency")]
public sealed class ConcurrentBookingTests
{
    private SchedulerWebAppFactory _factory = null!;
    private SeedData _seed = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new SchedulerWebAppFactory();
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        _seed = await TestSeed.SeedAsync(ctx);
    }

    [TearDown]
    public async Task TearDown() => await _factory.DisposeAsync();

    [Test]
    public async Task TenConcurrentBookings_ForSameSlot_TwoSucceedEightConflict()
    {
        // The seed has 2 technicians (Alice, Bob) and 2 bays at Dealership A with no required skills for QuickService.
        // 10 parallel bookings for the same slot should fill both resources exactly twice (2 successes),
        // leaving 8 conflicts. This proves the layered concurrency defenses prevent over-booking.
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            using var client = _factory.CreateClient();
            return await client.PostAsJsonAsync("/api/v1/appointments", req);
        }).ToArray();

        var results = await Task.WhenAll(tasks);

        var created  = results.Count(r => r.StatusCode == HttpStatusCode.Created);
        var conflict = results.Count(r => r.StatusCode == HttpStatusCode.Conflict);

        // Two technicians + two bays = capacity for 2 simultaneous bookings.
        created.Should().Be(2, "exactly the capacity for the slot must succeed");
        conflict.Should().Be(8, "all losers must surface a clean 409");

        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var confirmed = await ctx.Appointments
            .Where(a => a.StartsAtUtc == start && a.Status == AppointmentStatus.Confirmed)
            .CountAsync();
        confirmed.Should().Be(2);
    }

    [Test]
    public async Task ConcurrentBookAndCancel_DoesNotCorruptState()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        using var client = _factory.CreateClient();
        var bookRes = await client.PostAsJsonAsync("/api/v1/appointments", req);
        var booked = await bookRes.Content.ReadFromJsonAsync<AppointmentResponse>();

        var cancelTasks = Enumerable.Range(0, 5).Select(_ =>
            client.PostAsync($"/api/v1/appointments/{booked!.Id}/cancel", null)).ToArray();

        var results = await Task.WhenAll(cancelTasks);
        results.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
    }
}
