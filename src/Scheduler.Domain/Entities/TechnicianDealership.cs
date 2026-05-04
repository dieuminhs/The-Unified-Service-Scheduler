using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class TechnicianDealership : EntityBase
{
    public Guid TechnicianId { get; set; }
    public Guid DealershipId { get; set; }

    public Technician? Technician { get; set; }
    public Dealership? Dealership { get; set; }
}
