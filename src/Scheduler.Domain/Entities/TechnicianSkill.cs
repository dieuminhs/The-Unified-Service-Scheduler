using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class TechnicianSkill : EntityBase
{
    public Guid TechnicianId { get; set; }
    public Guid SkillId { get; set; }

    public Technician? Technician { get; set; }
    public Skill? Skill { get; set; }
}
