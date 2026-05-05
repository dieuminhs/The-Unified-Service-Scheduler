using FluentAssertions;
using Moq;
using NUnit.Framework;
using Scheduler.Application.Abstractions.Persistence;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Services;
using Scheduler.Application.Services.Abstractions;
using Scheduler.Domain.Abstractions;
using Scheduler.Domain.Entities;
using Scheduler.Domain.Exceptions;

namespace Scheduler.Application.UnitTests.Services;

[TestFixture]
public sealed class BookingServiceTests
{
    private static readonly DateTime Now = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);
    private static DateTime In(int hours) => Now.AddHours(hours);

    private Mock<IDealershipRepository> _dealershipRepo = null!;
    private Mock<ICustomerRepository> _customerRepo = null!;
    private Mock<IVehicleRepository> _vehicleRepo = null!;
    private Mock<IServiceTypeRepository> _serviceTypeRepo = null!;
    private Mock<IServiceBayRepository> _bayRepo = null!;
    private Mock<IQualificationMatcher> _qualifier = null!;
    private Mock<IAvailabilityService> _availability = null!;
    private Mock<IAppointmentWriter> _writer = null!;
    private Mock<IUnitOfWork> _uow = null!;
    private Mock<IClock> _clock = null!;
    private Mock<IOpeningHoursValidator> _hours = null!;
    private BookingService _sut = null!;

    private Dealership _dealership = null!;
    private Customer _customer = null!;
    private Vehicle _vehicle = null!;
    private ServiceType _service = null!;
    private Technician _tech = null!;
    private ServiceBay _bay = null!;

    [SetUp]
    public void SetUp()
    {
        _dealershipRepo = new();
        _customerRepo = new();
        _vehicleRepo = new();
        _serviceTypeRepo = new();
        _bayRepo = new();
        _qualifier = new();
        _availability = new();
        _writer = new();
        _uow = new();
        _clock = new();
        _hours = new();

        _clock.SetupGet(c => c.UtcNow).Returns(Now);
        _hours.Setup(h => h.IsWithinOpeningHours(It.IsAny<Dealership>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(true);

        _dealership = new Dealership { Name = "D" };
        _customer = new Customer { FirstName = "C", LastName = "X", Email = "c@x.example" };
        _vehicle = new Vehicle { CustomerId = _customer.Id, Vin = "VIN", Make = "M", Model = "X", Year = 2024 };
        _service = new ServiceType { Name = "S", DurationMinutes = 60 };
        _tech = new Technician { FullName = "T" };
        _bay = new ServiceBay { Name = "B" };

        _dealershipRepo.Setup(r => r.GetByIdAsync(_dealership.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_dealership);
        _customerRepo.Setup(r => r.GetByIdAsync(_customer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_customer);
        _vehicleRepo.Setup(r => r.GetByIdAsync(_vehicle.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vehicle);
        _serviceTypeRepo.Setup(r => r.GetByIdAsync(_service.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_service);
        _bayRepo.Setup(r => r.ListAtDealershipAsync(_dealership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ServiceBay> { _bay });
        _qualifier.Setup(q => q.FindQualifiedAsync(_service.Id, _dealership.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Technician> { _tech });
        _availability.Setup(a => a.FirstFreeTechnicianAsync(
                It.IsAny<IReadOnlyList<Technician>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_tech);
        _availability.Setup(a => a.FirstFreeBayAsync(
                It.IsAny<IReadOnlyList<ServiceBay>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(_bay);
        _uow.Setup(u => u.BeginSerializableTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IAsyncDisposable>());

        _sut = new BookingService(
            _dealershipRepo.Object,
            _customerRepo.Object,
            _vehicleRepo.Object,
            _serviceTypeRepo.Object,
            _bayRepo.Object,
            _qualifier.Object,
            _availability.Object,
            _writer.Object,
            _uow.Object,
            _clock.Object,
            _hours.Object);
    }

    private BookAppointmentRequest Req() => new(_dealership.Id, _customer.Id, _vehicle.Id, _service.Id, In(1));

    [Test]
    public async Task BookAsync_AllResourcesAvailable_ReturnsConfirmedAppointment()
    {
        var appointment = await _sut.BookAsync(Req(), CancellationToken.None);
        appointment.Should().NotBeNull();
        appointment.TechnicianId.Should().Be(_tech.Id);
        appointment.ServiceBayId.Should().Be(_bay.Id);
        appointment.EndsAtUtc.Should().Be(In(1).AddMinutes(60));
        _writer.Verify(w => w.AddAsync(It.IsAny<Appointment>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void BookAsync_StartInPast_ThrowsStartInPast()
    {
        var pastReq = Req() with { StartsAtUtc = In(-1) };
        var act = async () => await _sut.BookAsync(pastReq, CancellationToken.None);
        act.Should().ThrowAsync<StartInPastException>();
    }

    [Test]
    public void BookAsync_OutsideOpeningHours_ThrowsOutsideOpeningHours()
    {
        _hours.Setup(h => h.IsWithinOpeningHours(It.IsAny<Dealership>(), It.IsAny<DateTime>(), It.IsAny<DateTime>()))
            .Returns(false);
        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<OutsideOpeningHoursException>();
    }

    [Test]
    public void BookAsync_NoQualifiedTechnicians_ThrowsTechnicianUnqualified()
    {
        _qualifier.Setup(q => q.FindQualifiedAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Technician>());

        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<TechnicianUnqualifiedException>();
    }

    [Test]
    public void BookAsync_NoTechnicianFree_ThrowsSlotTaken()
    {
        _availability.Setup(a => a.FirstFreeTechnicianAsync(
                It.IsAny<IReadOnlyList<Technician>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Technician?)null);

        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<SlotTakenException>();
    }

    [Test]
    public void BookAsync_NoBayFree_ThrowsSlotTaken()
    {
        _availability.Setup(a => a.FirstFreeBayAsync(
                It.IsAny<IReadOnlyList<ServiceBay>>(),
                It.IsAny<DateTime>(),
                It.IsAny<DateTime>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ServiceBay?)null);

        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<SlotTakenException>();
    }

    [Test]
    public void BookAsync_MissingDealership_ThrowsResourceNotFound()
    {
        _dealershipRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Dealership?)null);
        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<ResourceNotFoundException>();
    }

    [Test]
    public void BookAsync_VehicleNotOwnedByCustomer_Throws()
    {
        var otherCustomer = new Customer { FirstName = "Z", LastName = "Z", Email = "z@z.example" };
        _vehicle.CustomerId = otherCustomer.Id;
        var act = async () => await _sut.BookAsync(Req(), CancellationToken.None);
        act.Should().ThrowAsync<ArgumentException>();
    }
}
