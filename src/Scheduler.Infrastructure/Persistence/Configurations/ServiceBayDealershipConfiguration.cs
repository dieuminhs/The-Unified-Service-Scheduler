using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class ServiceBayDealershipConfiguration : IEntityTypeConfiguration<ServiceBayDealership>
{
    public void Configure(EntityTypeBuilder<ServiceBayDealership> b)
    {
        b.ToTable("ServiceBayDealerships");
        b.HasKey(x => new { x.ServiceBayId, x.DealershipId });
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasOne(x => x.ServiceBay).WithMany(s => s.DealershipAssignments).HasForeignKey(x => x.ServiceBayId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Dealership).WithMany(d => d.ServiceBayAssignments).HasForeignKey(x => x.DealershipId).OnDelete(DeleteBehavior.Cascade);

        b.HasIndex(x => new { x.DealershipId, x.ServiceBayId })
            .HasDatabaseName("IX_ServiceBayDealership_Dealership")
            .HasFilter("IsDeleted = 0");
    }
}
