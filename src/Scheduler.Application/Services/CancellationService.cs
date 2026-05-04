using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.Services;

public sealed class CancellationService : ICancellationService
{
    private readonly IAppointmentWriter _writer;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;

    public CancellationService(IAppointmentWriter writer, IUnitOfWork uow, IClock clock)
    {
        _writer = writer;
        _uow = uow;
        _clock = clock;
    }

    public async Task<Appointment> CancelAsync(Guid appointmentId, CancellationToken ct)
    {
        var appointment = await _writer.GetForUpdateAsync(appointmentId, ct)
            ?? throw new ResourceNotFoundException(nameof(Appointment), appointmentId);

        try
        {
            appointment.Cancel(_clock.UtcNow);
        }
        catch (InvalidOperationException)
        {
            throw new AlreadyCancelledException();
        }

        await _uow.SaveChangesAsync(ct);
        return appointment;
    }
}
