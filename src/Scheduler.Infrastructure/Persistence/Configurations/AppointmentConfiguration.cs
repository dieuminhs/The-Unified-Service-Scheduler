using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> b)
    {
        b.ToTable("Appointments");
        b.HasKey(x => x.Id);

        b.Property(x => x.Status).HasConversion<int>().IsRequired();
        b.Property(x => x.StartsAtUtc).IsRequired();
        b.Property(x => x.EndsAtUtc).IsRequired();

        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasOne<Dealership>().WithMany().HasForeignKey(x => x.DealershipId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Customer>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Vehicle>().WithMany().HasForeignKey(x => x.VehicleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceType>().WithMany().HasForeignKey(x => x.ServiceTypeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Technician>().WithMany().HasForeignKey(x => x.TechnicianId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ServiceBay>().WithMany().HasForeignKey(x => x.ServiceBayId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.TechnicianId, x.StartsAtUtc })
            .IsUnique()
            .HasDatabaseName("UX_Appointment_Technician_Confirmed_Start")
            .HasFilter("Status = 1 AND IsDeleted = 0");

        b.HasIndex(x => new { x.ServiceBayId, x.StartsAtUtc })
            .IsUnique()
            .HasDatabaseName("UX_Appointment_Bay_Confirmed_Start")
            .HasFilter("Status = 1 AND IsDeleted = 0");

        b.HasIndex(x => new { x.TechnicianId, x.StartsAtUtc, x.EndsAtUtc })
            .HasDatabaseName("IX_Appointment_Tech_Window")
            .HasFilter("Status = 1 AND IsDeleted = 0");

        b.HasIndex(x => new { x.ServiceBayId, x.StartsAtUtc, x.EndsAtUtc })
            .HasDatabaseName("IX_Appointment_Bay_Window")
            .HasFilter("Status = 1 AND IsDeleted = 0");

        b.HasIndex(x => new { x.DealershipId, x.StartsAtUtc })
            .HasDatabaseName("IX_Appointment_Dealership_Start");
    }
}
