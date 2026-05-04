namespace Scheduler.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
    Task<IAsyncDisposable> BeginSerializableTransactionAsync(CancellationToken ct);
}
