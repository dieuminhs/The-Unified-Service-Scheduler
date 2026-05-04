using Scheduler.Domain.Common;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Domain.Entities;

public sealed class Dealership : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string TimeZone { get; set; } = "Etc/UTC";
    public List<OpeningHoursEntry> OpeningHours { get; set; } = new();

    public ICollection<TechnicianDealership> TechnicianAssignments { get; set; } = new List<TechnicianDealership>();
    public ICollection<ServiceBayDealership> ServiceBayAssignments { get; set; } = new List<ServiceBayDealership>();
}
