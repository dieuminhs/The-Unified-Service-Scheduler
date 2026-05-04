using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scheduler.Domain.Abstractions;
using Scheduler.Infrastructure.Persistence.Interceptors;

namespace Scheduler.Infrastructure.Persistence;

public sealed class SchedulerDbContextFactory : IDesignTimeDbContextFactory<SchedulerDbContext>
{
    public SchedulerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SchedulerDbContext>()
            .UseSqlite("Data Source=scheduler.design.db")
            .AddInterceptors(new AuditAndSoftDeleteInterceptor(new DesignTimeClock()))
            .Options;
        return new SchedulerDbContext(options);
    }

    private sealed class DesignTimeClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
