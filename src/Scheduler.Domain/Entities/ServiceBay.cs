using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceBay : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public ICollection<ServiceBayDealership> DealershipAssignments { get; set; } = new List<ServiceBayDealership>();
}
