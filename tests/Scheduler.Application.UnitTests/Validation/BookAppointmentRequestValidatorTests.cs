using FluentValidation.TestHelper;
using NUnit.Framework;
using Scheduler.Application.Contracts.Requests;
using Scheduler.Application.Validation;

namespace Scheduler.Application.UnitTests.Validation;

[TestFixture]
public sealed class BookAppointmentRequestValidatorTests
{
    [Test]
    public void Validate_EmptyIds_FailsValidation()
    {
        var sut = new BookAppointmentRequestValidator();
        var request = new BookAppointmentRequest(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            DateTime.UtcNow.AddHours(1));

        var result = sut.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.DealershipId);
        result.ShouldHaveValidationErrorFor(x => x.CustomerId);
        result.ShouldHaveValidationErrorFor(x => x.VehicleId);
        result.ShouldHaveValidationErrorFor(x => x.ServiceTypeId);
    }

    [Test]
    public void Validate_ValidRequest_Passes()
    {
        var sut = new BookAppointmentRequestValidator();
        var request = new BookAppointmentRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTime.UtcNow.AddHours(1));

        sut.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}
