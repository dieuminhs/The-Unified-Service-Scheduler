using FluentValidation;
using Scheduler.Application.Contracts.Requests;

namespace Scheduler.Application.Validation;

public sealed class AvailabilityQueryRequestValidator : AbstractValidator<AvailabilityQueryRequest>
{
    public AvailabilityQueryRequestValidator()
    {
        RuleFor(x => x.DealershipId).NotEmpty();
        RuleFor(x => x.ServiceTypeId).NotEmpty();
        RuleFor(x => x.GranularityMinutes).GreaterThanOrEqualTo(5).LessThanOrEqualTo(120);
        RuleFor(x => x.ToUtc).GreaterThan(x => x.FromUtc);
    }
}
