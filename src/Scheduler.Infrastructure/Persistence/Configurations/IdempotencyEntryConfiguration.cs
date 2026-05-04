using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Scheduler.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyEntryConfiguration : IEntityTypeConfiguration<IdempotencyEntry>
{
    public void Configure(EntityTypeBuilder<IdempotencyEntry> b)
    {
        b.ToTable("IdempotencyEntries");
        b.HasKey(x => x.Key);
        b.Property(x => x.Key).HasMaxLength(128);
        b.Property(x => x.BodyHash).IsRequired().HasMaxLength(64);
        b.Property(x => x.ResponseStatusCode).IsRequired().HasMaxLength(8);
        b.Property(x => x.ResponseBody).IsRequired().HasColumnType("TEXT");
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("IX_IdempotencyEntries_ExpiresAtUtc");
    }
}
