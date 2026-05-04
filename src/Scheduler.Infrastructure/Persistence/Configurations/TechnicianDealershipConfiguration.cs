using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class TechnicianDealershipConfiguration : IEntityTypeConfiguration<TechnicianDealership>
{
    public void Configure(EntityTypeBuilder<TechnicianDealership> b)
    {
        b.ToTable("TechnicianDealerships");
        b.HasKey(x => new { x.TechnicianId, x.DealershipId });
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne(x => x.Technician).WithMany(t => t.DealershipAssignments).HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Dealership).WithMany(d => d.TechnicianAssignments).HasForeignKey(x => x.DealershipId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.DealershipId, x.TechnicianId })
            .HasDatabaseName("IX_TechnicianDealership_Dealership")
            .HasFilter("IsDeleted = 0");
    }
}
