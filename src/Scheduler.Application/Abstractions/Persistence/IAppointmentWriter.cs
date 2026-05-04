using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IAppointmentWriter
{
    Task AddAsync(Appointment appointment, CancellationToken ct);
    Task<bool> AnyTechnicianOverlapAsync(Guid technicianId, DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<bool> AnyBayOverlapAsync(Guid bayId, DateTime startUtc, DateTime endUtc, CancellationToken ct);
    Task<Appointment?> GetForUpdateAsync(Guid id, CancellationToken ct);
}
