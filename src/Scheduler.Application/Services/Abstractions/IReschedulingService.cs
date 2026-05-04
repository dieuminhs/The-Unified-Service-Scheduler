using Scheduler.Application.Contracts.Requests;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IReschedulingService
{
    Task<Appointment> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct);
}
