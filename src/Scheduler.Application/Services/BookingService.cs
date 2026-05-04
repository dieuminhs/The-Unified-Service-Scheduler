using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.Services;

public sealed class BookingService : IBookingService
{
    private readonly IDealershipRepository _dealershipRepo;
    private readonly ICustomerRepository _customerRepo;
    private readonly IVehicleRepository _vehicleRepo;
    private readonly IServiceTypeRepository _serviceTypeRepo;
    private readonly IServiceBayRepository _bayRepo;
    private readonly IQualificationMatcher _qualifier;
    private readonly IAvailabilityService _availability;
    private readonly IAppointmentWriter _writer;
    private readonly IUnitOfWork _uow;
    private readonly IClock _clock;
    private readonly IOpeningHoursValidator _hours;

    public BookingService(
        IDealershipRepository dealershipRepo,
        ICustomerRepository customerRepo,
        IVehicleRepository vehicleRepo,
        IServiceTypeRepository serviceTypeRepo,
        IServiceBayRepository bayRepo,
        IQualificationMatcher qualifier,
        IAvailabilityService availability,
        IAppointmentWriter writer,
        IUnitOfWork uow,
        IClock clock,
        IOpeningHoursValidator hours)
    {
        _dealershipRepo = dealershipRepo;
        _customerRepo = customerRepo;
        _vehicleRepo = vehicleRepo;
        _serviceTypeRepo = serviceTypeRepo;
        _bayRepo = bayRepo;
        _qualifier = qualifier;
        _availability = availability;
        _writer = writer;
        _uow = uow;
        _clock = clock;
        _hours = hours;
    }

    public async Task<Appointment> BookAsync(BookAppointmentRequest request, CancellationToken ct)
    {
        if (request.StartsAtUtc <= _clock.UtcNow)
        {
            throw new StartInPastException();
        }

        await using var _ = await _uow.BeginSerializableTransactionAsync(ct);

        var dealership = await _dealershipRepo.GetByIdAsync(request.DealershipId, ct)
            ?? throw new ResourceNotFoundException(nameof(Dealership), request.DealershipId);
        var customer = await _customerRepo.GetByIdAsync(request.CustomerId, ct)
            ?? throw new ResourceNotFoundException(nameof(Customer), request.CustomerId);
        var vehicle = await _vehicleRepo.GetByIdAsync(request.VehicleId, ct)
            ?? throw new ResourceNotFoundException(nameof(Vehicle), request.VehicleId);
        var serviceType = await _serviceTypeRepo.GetByIdAsync(request.ServiceTypeId, ct)
            ?? throw new ResourceNotFoundException(nameof(ServiceType), request.ServiceTypeId);

        if (vehicle.CustomerId != customer.Id)
        {
            throw new ArgumentException("Vehicle does not belong to the specified customer.");
        }

        var endsAtUtc = request.StartsAtUtc.AddMinutes(serviceType.DurationMinutes);
        if (!_hours.IsWithinOpeningHours(dealership, request.StartsAtUtc, endsAtUtc))
        {
            throw new OutsideOpeningHoursException();
        }

        var qualifiedTechnicians = await _qualifier.FindQualifiedAsync(request.ServiceTypeId, request.DealershipId, ct);
        if (qualifiedTechnicians.Count == 0)
        {
            throw new TechnicianUnqualifiedException();
        }

        var technician = await _availability.FirstFreeTechnicianAsync(qualifiedTechnicians, request.StartsAtUtc, endsAtUtc, ct)
            ?? throw new SlotTakenException();

        var bays = await _bayRepo.ListAtDealershipAsync(request.DealershipId, ct);
        var bay = await _availability.FirstFreeBayAsync(bays, request.StartsAtUtc, endsAtUtc, ct)
            ?? throw new SlotTakenException();

        var appointment = Appointment.Confirm(
            dealershipId: request.DealershipId,
            customerId: request.CustomerId,
            vehicleId: request.VehicleId,
            serviceTypeId: request.ServiceTypeId,
            technicianId: technician.Id,
            serviceBayId: bay.Id,
            startsAtUtc: request.StartsAtUtc,
            endsAtUtc: endsAtUtc);

        await _writer.AddAsync(appointment, ct);
        await _uow.SaveChangesAsync(ct);

        return appointment;
    }
}
