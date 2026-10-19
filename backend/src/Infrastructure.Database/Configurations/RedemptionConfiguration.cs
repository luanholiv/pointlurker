using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;
using PointLurker.Infrastructure.Database.ValueConverters;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class RedemptionConfiguration : IEntityTypeConfiguration<Redemption>
{
    public void Configure(EntityTypeBuilder<Redemption> builder)
    {
        builder.ToTable("redemptions");
        builder.HasKey(redemption => redemption.Id);

        // Snapshotted at creation: a redemption outlives the store it came from.
        builder.Property(redemption => redemption.Provider).HasConversion<int>().IsRequired();

        builder
            .Property(redemption => redemption.StoreExternalId)
            .HasConversion(ValueObjectConverters.StoreExternalIdConverter)
            .HasMaxLength(64)
            .IsRequired();

        builder
            .Property(redemption => redemption.ProviderItemId)
            .HasConversion(ValueObjectConverters.ProviderItemIdConverter)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(redemption => redemption.StoreItemName).HasMaxLength(256).IsRequired();

        builder.Property(redemption => redemption.StoreName).HasMaxLength(200).IsRequired();

        builder
            .Property(redemption => redemption.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        // FailureCode: new int-backed enum column (nullable — only set for terminal failure status).
        builder
            .Property(redemption => redemption.FailureCode)
            .HasConversion<int?>()
            .IsRequired(false)
            .HasColumnType("integer");

        // FailureContext: non-localizable diagnostic detail, never shown to end users.
        builder
            .Property(redemption => redemption.FailureContext)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(redemption => redemption.ProviderRedemptionId).HasMaxLength(128);

        builder.Property(redemption => redemption.SubmittedAt).IsRequired(false);
        builder.Property(redemption => redemption.SettledAt).IsRequired(false);
        builder.Property(redemption => redemption.NextRetryAt).IsRequired(false);
        builder.Property(redemption => redemption.Attempts).IsRequired().HasDefaultValue(0);

        // PollFailures / LastPollError: how the completion check is faring, not how the submission
        // went. Kept beside the row so a permanently unreadable status is visible while it happens.
        builder.Property(redemption => redemption.PollFailures).IsRequired().HasDefaultValue(0);

        builder
            .Property(redemption => redemption.LastPollError)
            .HasMaxLength(512)
            .IsRequired(false);

        builder
            .HasIndex(r => new { r.Status, r.NextRetryAt })
            .HasDatabaseName("IX_redemptions_status_next_retry");

        builder
            .HasIndex(r => new { r.Status, r.SubmittedAt })
            .HasDatabaseName("IX_redemptions_status_submitted");
    }
}
