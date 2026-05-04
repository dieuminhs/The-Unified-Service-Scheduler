using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class ServiceTypeRequiredSkillConfiguration : IEntityTypeConfiguration<ServiceTypeRequiredSkill>
{
    public void Configure(EntityTypeBuilder<ServiceTypeRequiredSkill> b)
    {
        b.ToTable("ServiceTypeRequiredSkills");
        b.HasKey(x => new { x.ServiceTypeId, x.SkillId });
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.ServiceType).WithMany(s => s.RequiredSkills).HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
    }
}
