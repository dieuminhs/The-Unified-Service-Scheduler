using Scheduler.Domain.Entities;

namespace Scheduler.Application.Services.Abstractions;

public interface IOpeningHoursValidator
{
    bool IsWithinOpeningHours(Dealership dealership, DateTime startUtc, DateTime endUtc);
}
