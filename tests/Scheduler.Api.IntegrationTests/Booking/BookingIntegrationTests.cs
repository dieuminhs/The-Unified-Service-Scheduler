using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Scheduler.Api.IntegrationTests.Fixtures;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Booking;

[TestFixture]
public sealed class BookingIntegrationTests
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
    public async Task Book_HappyPath_Returns201_Confirmed()
    {
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, _factory.Clock.UtcNow.AddHours(1));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await res.Content.ReadFromJsonAsync<AppointmentResponse>();
        body!.Status.Should().Be("Confirmed");
        body.EndsAtUtc.Should().Be(req.StartsAtUtc.AddMinutes(30));
    }

    [Test]
    public async Task Book_StartInPast_Returns422_StartInPast()
    {
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, _factory.Clock.UtcNow.AddMinutes(-30));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("START_IN_PAST");
    }

    [Test]
    public async Task Book_NoQualifiedTechnician_Returns422_TechnicianUnqualified()
    {
        // EV service requires both EV_CERT and DIAG; only Alice has both, and she's at A.
        // Booking against B (where Bob is, but he lacks EV_CERT) should yield TECHNICIAN_UNQUALIFIED.
        var req = new BookAppointmentRequest(_seed.DealershipB.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.EvService.Id, _factory.Clock.UtcNow.AddHours(1));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var problem = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("TECHNICIAN_UNQUALIFIED");
    }

    [Test]
    public async Task Book_AllResourcesBusy_Returns409_SlotTaken()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);

        // Attempt 1: succeeds (Alice/Bay1)
        await _client.PostAsJsonAsync("/api/v1/appointments",
            new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id, _seed.QuickService.Id, start));

        // Attempt 2: succeeds (Bob/Bay2)
        await _client.PostAsJsonAsync("/api/v1/appointments",
            new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id, _seed.QuickService.Id, start));

        // Attempt 3: should fail - only 2 techs and 2 bays at A
        var third = await _client.PostAsJsonAsync("/api/v1/appointments",
            new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id, _seed.QuickService.Id, start));

        third.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await third.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("SLOT_TAKEN");
    }

    [Test]
    public async Task Book_VehicleBelongsToDifferentCustomer_Returns400_OrEquivalent()
    {
        var otherCustomerId = Guid.NewGuid();
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, otherCustomerId, _seed.Vehicle.Id,
            _seed.QuickService.Id, _factory.Clock.UtcNow.AddHours(1));

        var res = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        res.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.UnprocessableEntity, HttpStatusCode.BadRequest);
    }
}
