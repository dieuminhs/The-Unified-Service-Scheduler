using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IAvailabilityService
{
    Task<IReadOnlyList<AvailabilitySlotResponse>> ListSlotsAsync(AvailabilityQueryRequest q, CancellationToken ct);

    Task<Technician?> FirstFreeTechnicianAsync(
        IReadOnlyList<Technician> qualified,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct);

    Task<ServiceBay?> FirstFreeBayAsync(
        IReadOnlyList<ServiceBay> bays,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct);
}
