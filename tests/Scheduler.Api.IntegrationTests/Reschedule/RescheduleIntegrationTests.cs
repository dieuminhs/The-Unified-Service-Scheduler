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

namespace Scheduler.Api.IntegrationTests.Reschedule;

[TestFixture]
public sealed class RescheduleIntegrationTests
{
    private SchedulerWebAppFactory _factory = null!;
    private HttpClient _client = null!;
    private SeedData _seed = null!;

    [SetUp]
    public async Task SetUp()
    {
        _factory = new SchedulerWebAppFactory();
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        await ctx.Database.EnsureCreatedAsync();
        _seed = await TestSeed.SeedAsync(ctx);
        _client = _factory.CreateClient();
    }

    [TearDown]
    public async Task TearDown()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Test]
    public async Task Reschedule_HappyPath_OldCancelledNewConfirmed()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var firstRes = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        var first = await firstRes.Content.ReadFromJsonAsync<AppointmentResponse>();

        var newStart = start.AddHours(2);
        var resched = await _client.PostAsJsonAsync($"/api/v1/appointments/{first!.Id}/reschedule",
            new RescheduleAppointmentRequest(newStart));
        resched.StatusCode.Should().Be(HttpStatusCode.OK);

        var newAppt = await resched.Content.ReadFromJsonAsync<AppointmentResponse>();
        newAppt!.StartsAtUtc.Should().Be(newStart);

        // Old appointment is cancelled
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SchedulerDbContext>();
        var oldFromDb = await ctx.Appointments.FirstAsync(a => a.Id == first.Id);
        oldFromDb.Status.Should().Be(AppointmentStatus.Cancelled);
    }
}
