using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceTypeRequiredSkill : EntityBase
{
    public Guid ServiceTypeId { get; set; }
    public Guid SkillId { get; set; }

    public ServiceType? ServiceType { get; set; }
    public Skill? Skill { get; set; }
}
