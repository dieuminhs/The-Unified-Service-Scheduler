using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Common;

namespace Scheduler.Infrastructure.Persistence.Interceptors;

public sealed class AuditAndSoftDeleteInterceptor : SaveChangesInterceptor
{
    private readonly IClock _clock;

    public AuditAndSoftDeleteInterceptor(IClock clock) => _clock = clock;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, ct);
    }

    private void Apply(DbContext? db)
    {
        if (db is null) return;
        var now = _clock.UtcNow;

        foreach (var entry in db.ChangeTracker.Entries<EntityBase>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAtUtc = now;
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAtUtc = now;
                    entry.Entity.UpdatedAtUtc = now;
                    entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                    break;
            }
        }
    }
}
