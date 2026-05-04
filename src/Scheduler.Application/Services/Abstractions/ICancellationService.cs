using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface ICancellationService
{
    Task<Appointment> CancelAsync(Guid appointmentId, CancellationToken ct);
}
