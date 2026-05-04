using Scheduler.Application.Contracts.Requests;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IBookingService
{
    Task<Appointment> BookAsync(BookAppointmentRequest request, CancellationToken ct);
}
