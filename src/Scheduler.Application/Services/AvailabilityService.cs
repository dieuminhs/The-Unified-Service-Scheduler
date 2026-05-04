using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Contracts.Responses;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.Services;

public sealed class AvailabilityService : IAvailabilityService
{
    private readonly IAppointmentWriter _writer;
    private readonly IDealershipRepository _dealershipRepo;
    private readonly IServiceTypeRepository _serviceTypeRepo;
    private readonly IServiceBayRepository _bayRepo;
    private readonly ITechnicianRepository _techRepo;

    public AvailabilityService(
        IAppointmentWriter writer,
        IDealershipRepository dealershipRepo,
        IServiceTypeRepository serviceTypeRepo,
        IServiceBayRepository bayRepo,
        ITechnicianRepository techRepo)
    {
        _writer = writer;
        _dealershipRepo = dealershipRepo;
        _serviceTypeRepo = serviceTypeRepo;
        _bayRepo = bayRepo;
        _techRepo = techRepo;
    }

    public async Task<Technician?> FirstFreeTechnicianAsync(
        IReadOnlyList<Technician> qualified,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct)
    {
        foreach (var technician in qualified)
        {
            if (!await _writer.AnyTechnicianOverlapAsync(technician.Id, startUtc, endUtc, ct))
            {
                return technician;
            }
        }

        return null;
    }

    public async Task<ServiceBay?> FirstFreeBayAsync(
        IReadOnlyList<ServiceBay> bays,
        DateTime startUtc,
        DateTime endUtc,
        CancellationToken ct)
    {
        foreach (var bay in bays)
        {
            if (!await _writer.AnyBayOverlapAsync(bay.Id, startUtc, endUtc, ct))
            {
                return bay;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<AvailabilitySlotResponse>> ListSlotsAsync(
        AvailabilityQueryRequest q,
        CancellationToken ct)
    {
        var dealership = await _dealershipRepo.GetByIdAsync(q.DealershipId, ct)
            ?? throw new ResourceNotFoundException(nameof(Dealership), q.DealershipId);
        var serviceType = await _serviceTypeRepo.GetByIdAsync(q.ServiceTypeId, ct)
            ?? throw new ResourceNotFoundException(nameof(ServiceType), q.ServiceTypeId);

        var bays = await _bayRepo.ListAtDealershipAsync(dealership.Id, ct);
        var technicians = await _techRepo.ListQualifiedForServiceAtDealershipAsync(serviceType.Id, dealership.Id, ct);

        var slots = new List<AvailabilitySlotResponse>();
        var step = TimeSpan.FromMinutes(q.GranularityMinutes);
        var duration = TimeSpan.FromMinutes(serviceType.DurationMinutes);

        for (var start = q.FromUtc; start + duration <= q.ToUtc; start += step)
        {
            var end = start + duration;
            var freeTechnician = await FirstFreeTechnicianAsync(technicians, start, end, ct);
            if (freeTechnician is null)
            {
                continue;
            }

            var freeBay = await FirstFreeBayAsync(bays, start, end, ct);
            if (freeBay is null)
            {
                continue;
            }

            slots.Add(new AvailabilitySlotResponse(start, end));
        }

        return slots;
    }
}
