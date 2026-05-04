using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.Services;

public sealed class ReschedulingService : IReschedulingService
{
    private readonly IAppointmentWriter _writer;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IBookingService _booking;

    public ReschedulingService(IAppointmentWriter writer, IUnitOfWork uow, IClock clock, IBookingService booking)
    {
        _writer = writer;
        _uow = uow;
        _clock = clock;
        _booking = booking;
    }

    public async Task<Appointment> RescheduleAsync(Guid appointmentId, RescheduleAppointmentRequest request, CancellationToken ct)
    {
        await using var _ = await _uow.BeginSerializableTransactionAsync(ct);

        var existing = await _writer.GetForUpdateAsync(appointmentId, ct)
            ?? throw new ResourceNotFoundException(nameof(Appointment), appointmentId);

        try
        {
            existing.Cancel(_clock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            throw new AlreadyCancelledException();
        }

        await _uow.SaveChangesAsync(ct);

        var rebookRequest = new BookAppointmentRequest(
            existing.DealershipId,
            existing.CustomerId,
            existing.VehicleId,
            existing.ServiceTypeId,
            request.NewStartsAtUtc);

        return await _booking.BookAsync(rebookRequest, ct);
    }
}
