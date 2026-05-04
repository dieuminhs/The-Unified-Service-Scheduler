using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scheduler.Domain.Entities;
using Scheduler.Domain.ValueObjects;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class DealershipConfiguration : IEntityTypeConfiguration<Dealership>
{
    public void Configure(EntityTypeBuilder<Dealership> b)
    {
        b.ToTable("Dealerships");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).IsRequired().HasMaxLength(200);
        b.Property(x => x.TimeZone).IsRequired().HasMaxLength(64);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.Property(x => x.OpeningHours)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<OpeningHoursEntry>>(v, (JsonSerializerOptions?)null) ?? new())
            .HasColumnType("TEXT");
    }
}
