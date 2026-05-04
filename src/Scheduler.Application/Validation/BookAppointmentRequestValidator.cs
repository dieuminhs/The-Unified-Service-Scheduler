using FluentValidation;
using Scheduler.Application.Contracts.Requests;

namespace Scheduler.Application.Validation;

public sealed class BookAppointmentRequestValidator : AbstractValidator<BookAppointmentRequest>
{
    public BookAppointmentRequestValidator()
    {
        RuleFor(x => x.DealershipId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty();
        RuleFor(x => x.VehicleId).NotEmpty();
        RuleFor(x => x.ServiceTypeId).NotEmpty();
        RuleFor(x => x.StartsAtUtc)
            .Must(s => s.Kind is DateTimeKind.Utc or DateTimeKind.Unspecified)
            .WithMessage("StartsAtUtc must be UTC.");
    }
}
