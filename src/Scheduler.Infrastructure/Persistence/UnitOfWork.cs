using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Scheduler.Application.Abstractions.Persistence;

namespace Scheduler.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly SchedulerDbContext _ctx;
    public UnitOfWork(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<int> SaveChangesAsync(CancellationToken ct) => _ctx.SaveChangesAsync(ct);

    public async Task<IAsyncDisposable> BeginSerializableTransactionAsync(CancellationToken ct)
    {
        var txn = await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        return new TransactionScope(txn);
    }

    private sealed class TransactionScope : IAsyncDisposable
    {
        private readonly IDbContextTransaction _txn;
        public TransactionScope(IDbContextTransaction txn) => _txn = txn;
        public async ValueTask DisposeAsync()
        {
            await _txn.CommitAsync();
            await _txn.DisposeAsync();
        }
    }
}
