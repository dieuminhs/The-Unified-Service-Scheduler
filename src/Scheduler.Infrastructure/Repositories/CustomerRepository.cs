using Microsoft.EntityFrameworkCore;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Domain.Entities;
using Scheduler.Infrastructure.Persistence;

namespace Scheduler.Infrastructure.Repositories;

public sealed class CustomerRepository : ICustomerRepository
{
    private readonly SchedulerDbContext _ctx;
    public CustomerRepository(SchedulerDbContext ctx) => _ctx = ctx;

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        _ctx.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id && c.IsActive, ct);
}
