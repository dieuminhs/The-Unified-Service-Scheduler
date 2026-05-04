using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class TechnicianSkillConfiguration : IEntityTypeConfiguration<TechnicianSkill>
{
    public void Configure(EntityTypeBuilder<TechnicianSkill> b)
    {
        b.ToTable("TechnicianSkills");
        b.HasKey(x => new { x.TechnicianId, x.SkillId });
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasOne(x => x.Technician).WithMany(t => t.Skills).HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Skill).WithMany().HasForeignKey(x => x.SkillId).OnDelete(DeleteBehavior.Cascade);
    }
}
