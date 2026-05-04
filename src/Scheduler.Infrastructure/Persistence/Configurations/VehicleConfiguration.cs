using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> b)
    {
        b.ToTable("Vehicles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Vin).IsRequired().HasMaxLength(32);
        b.Property(x => x.Make).IsRequired().HasMaxLength(60);
        b.Property(x => x.Model).IsRequired().HasMaxLength(60);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasOne(x => x.Customer).WithMany(c => c.Vehicles).HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.Vin).IsUnique().HasDatabaseName("UX_Vehicle_Vin").HasFilter("IsDeleted = 0");
    }
}
