using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceBayDealership : EntityBase
{
    public Guid ServiceBayId { get; set; }
    public Guid DealershipId { get; set; }

    public ServiceBay? ServiceBay { get; set; }
    public Dealership? Dealership { get; set; }
}
