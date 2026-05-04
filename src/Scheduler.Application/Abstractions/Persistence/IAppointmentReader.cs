using Scheduler.Domain.Entities;

namespace Scheduler.Application.Abstractions.Persistence;

public interface IAppointmentReader
{
    Task<Appointment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Appointment>> ListAsync(
        Guid? dealershipId, Guid? customerId, Guid? technicianId,
        DateTime? fromUtc, DateTime? toUtc, int skip, int take, CancellationToken ct);
}
