using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class StoreInputResolutionLogEntryConfiguration
    : IEntityTypeConfiguration<StoreInputResolutionLogEntry>
{
    public void Configure(EntityTypeBuilder<StoreInputResolutionLogEntry> builder)
    {
        builder.ToTable("store_input_resolution_log");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.StoreId).IsRequired();
        builder.Property(e => e.RawLabel).HasMaxLength(512).IsRequired();
        builder.Property(e => e.CanonicalLabel).HasMaxLength(512);
        builder.Property(e => e.Confidence).IsRequired();
        builder.Property(e => e.Source).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(e => e.ObservedAt).HasColumnType("timestamptz").IsRequired();

        builder.HasIndex(e => new { e.StoreId, e.ObservedAt });
        builder.HasIndex(e => new
        {
            e.StoreId,
            e.RawLabel,
            e.ObservedAt,
        });

        builder
            .HasOne<WatchedStore>()
            .WithMany()
            .HasForeignKey(e => e.StoreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
