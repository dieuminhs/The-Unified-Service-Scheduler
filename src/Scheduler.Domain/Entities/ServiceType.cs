using Scheduler.Domain.Common;

namespace Scheduler.Domain.Entities;

public sealed class ServiceType : EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public string? Description { get; set; }

    public ICollection<ServiceTypeRequiredSkill> RequiredSkills { get; set; } = new List<ServiceTypeRequiredSkill>();
}
