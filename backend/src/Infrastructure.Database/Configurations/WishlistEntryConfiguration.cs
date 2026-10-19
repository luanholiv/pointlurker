using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class WishlistEntryConfiguration : IEntityTypeConfiguration<WishlistEntry>
{
    public void Configure(EntityTypeBuilder<WishlistEntry> builder)
    {
        builder.ToTable("wishlist_entries");
        builder.HasKey(entry => entry.Id);

        // Holds ciphertext (app-layer encrypted), which exceeds the former varchar(1000); plaintext
        // length is capped at WishlistEntry.RedemptionMessageMaxLength by the application layer.
        builder.Property(entry => entry.RedemptionMessage).HasColumnType("text");

        builder.Property(entry => entry.LastRedemptionCreatedAt).HasColumnType("timestamptz");

        // Nullable because null is a value here, not an absence: it means unlimited. Existing rows
        // were backfilled to 1 by the migration rather than left null — this column authorises
        // spending, so it fails closed.
        builder.Property(entry => entry.DesiredRedemptions);

        builder.HasIndex(entry => new { entry.UserId, entry.StoreItemId }).IsUnique();
    }
}
