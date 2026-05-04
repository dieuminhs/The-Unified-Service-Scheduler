using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> b)
    {
        b.ToTable("Customers");
        b.HasKey(x => x.Id);
        b.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
        b.Property(x => x.LastName).IsRequired().HasMaxLength(100);
        b.Property(x => x.Email).IsRequired().HasMaxLength(254);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasIndex(x => x.Email)
            .IsUnique()
            .HasDatabaseName("UX_Customer_Email")
            .HasFilter("IsDeleted = 0");
    }
}
