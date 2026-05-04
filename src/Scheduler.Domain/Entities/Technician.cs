using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class Technician : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;

    public ICollection<TechnicianDealership> DealershipAssignments { get; set; } = new List<TechnicianDealership>();
    public ICollection<TechnicianSkill> Skills { get; set; } = new List<TechnicianSkill>();
}
