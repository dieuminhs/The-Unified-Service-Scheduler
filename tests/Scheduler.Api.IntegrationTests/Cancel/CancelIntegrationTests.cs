using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Scheduler.Api.IntegrationTests.Fixtures;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Api.IntegrationTests.Cancel;

[TestFixture]
public sealed class CancelIntegrationTests
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
    public async Task Cancel_then_rebook_same_slot_succeeds()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var firstRes = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        var first = await firstRes.Content.ReadFromJsonAsync<AppointmentResponse>();

        var cancel = await _client.PostAsync($"/api/v1/appointments/{first!.Id}/cancel", null);
        cancel.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var second = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test]
    public async Task Cancel_already_cancelled_returns_409()
    {
        var start = _factory.Clock.UtcNow.AddHours(1);
        var req = new BookAppointmentRequest(_seed.DealershipA.Id, _seed.Customer.Id, _seed.Vehicle.Id,
            _seed.QuickService.Id, start);

        var firstRes = await _client.PostAsJsonAsync("/api/v1/appointments", req);
        var first = await firstRes.Content.ReadFromJsonAsync<AppointmentResponse>();

        await _client.PostAsync($"/api/v1/appointments/{first!.Id}/cancel", null);
        var again = await _client.PostAsync($"/api/v1/appointments/{first.Id}/cancel", null);

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var problem = await again.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        problem.GetProperty("code").GetString().Should().Be("ALREADY_CANCELLED");
    }
}
