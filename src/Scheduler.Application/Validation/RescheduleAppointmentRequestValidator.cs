using FluentValidation;
using Scheduler.Application.Contracts.Requests;

namespace Scheduler.Application.Validation;

public sealed class RescheduleAppointmentRequestValidator : AbstractValidator<RescheduleAppointmentRequest>
{
    public RescheduleAppointmentRequestValidator()
    {
        RuleFor(x => x.NewStartsAtUtc)
            .Must(s => s.Kind is DateTimeKind.Utc or DateTimeKind.Unspecified)
            .WithMessage("NewStartsAtUtc must be UTC.");
    }
}
