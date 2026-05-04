using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken ct);
}
