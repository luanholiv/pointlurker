using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class FulfillmentCodeConfiguration : IEntityTypeConfiguration<FulfillmentCode>
{
    public void Configure(EntityTypeBuilder<FulfillmentCode> builder)
    {
        builder.ToTable("fulfillment_codes");
        builder.HasKey(code => code.Id);

        // Ciphertext, unbounded: its length follows the Data Protection payload, not the code.
        builder.Property(code => code.EncryptedCode).IsRequired();
        builder.Property(code => code.CreatedAt).IsRequired();
        builder.Property(code => code.SentAt).IsRequired(false);
        builder.Property(code => code.Attempts).IsRequired().HasDefaultValue(0);
        builder.Property(code => code.NextRetryAt).IsRequired(false);
        builder
            .Property(code => code.LastError)
            .HasMaxLength(FulfillmentCode.MaxLastErrorLength)
            .IsRequired(false);

        // No navigation on either side: one on Redemption would put the secret back on the aggregate
        // every history read loads, and mutable delivery state back on an append-only snapshot.
        // Cascade matches the one existing precedent; redemptions are never deleted, so it guards a
        // future change rather than a current path.
        builder
            .HasOne<Redemption>()
            .WithOne()
            .HasForeignKey<FulfillmentCode>(code => code.RedemptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // A safety property, not decoration: a redemption submitted twice that came back with two
        // codes fails the second insert loudly instead of storing both.
        builder.HasIndex(code => code.RedemptionId).IsUnique();

        // The sweep's own index. Filtered to the undelivered rows, which stay few while the table
        // itself only grows.
        builder
            .HasIndex(code => code.NextRetryAt)
            .HasFilter("\"SentAt\" IS NULL")
            .HasDatabaseName("IX_fulfillment_codes_undelivered_next_retry");
    }
}
