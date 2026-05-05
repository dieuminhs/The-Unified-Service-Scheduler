using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;
using Scheduler.Infrastructure.Persistence.Interceptors;

namespace Scheduler.Application.UnitTests.Persistence;

[TestFixture]
public sealed class AuditAndSoftDeleteInterceptorTests
{
    private SqliteConnection _connection = null!;
    private SchedulerDbContext _ctx = null!;
    private Mock<IClock> _clock = null!;
    private DateTime _now;

    [SetUp]
    public void SetUp()
    {
        _now = new DateTime(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
        _clock = new Mock<IClock>();
        _clock.SetupGet(c => c.UtcNow).Returns(() => _now);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new AuditAndSoftDeleteInterceptor(_clock.Object))
            .Options;

        _ctx = new SchedulerDbContext(options);
        _ctx.Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _ctx.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task SaveChanges_AddedEntity_StampsCreatedAtUtc()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();

        c.CreatedAtUtc.Should().Be(_now);
        c.UpdatedAtUtc.Should().BeNull();
    }

    [Test]
    public async Task SaveChanges_ModifiedEntity_StampsUpdatedAtUtc()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();

        _now = _now.AddMinutes(5);
        c.FirstName = "Aaron";
        await _ctx.SaveChangesAsync();

        c.UpdatedAtUtc.Should().Be(_now);
    }

    [Test]
    public async Task SaveChanges_RemovedEntity_SoftDeletes()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();

        _now = _now.AddMinutes(5);
        _ctx.Customers.Remove(c);
        await _ctx.SaveChangesAsync();

        var fromDb = await _ctx.Customers.IgnoreQueryFilters().SingleAsync(x => x.Id == c.Id);
        fromDb.IsDeleted.Should().BeTrue();
        fromDb.DeletedAtUtc.Should().Be(_now);
    }

    [Test]
    public async Task Query_SoftDeletedRows_ExcludedByDefault()
    {
        var c = new Customer { FirstName = "A", LastName = "B", Email = "a@b.example" };
        _ctx.Customers.Add(c);
        await _ctx.SaveChangesAsync();
        _ctx.Customers.Remove(c);
        await _ctx.SaveChangesAsync();

        (await _ctx.Customers.AnyAsync()).Should().BeFalse();
        (await _ctx.Customers.IgnoreQueryFilters().AnyAsync()).Should().BeTrue();
    }
}
