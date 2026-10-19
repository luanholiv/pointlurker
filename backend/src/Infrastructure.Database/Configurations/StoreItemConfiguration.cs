using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;
using PointLurker.Infrastructure.Database.ValueConverters;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class StoreItemConfiguration : IEntityTypeConfiguration<StoreItem>
{
    public void Configure(EntityTypeBuilder<StoreItem> builder)
    {
        builder.ToTable("store_items");
        builder.HasKey(item => item.Id);

        // Denormalised from the owning store so item queries can filter by provider without a join.
        builder.Property(item => item.Provider).HasConversion<int>().IsRequired();

        builder
            .Property(item => item.ProviderItemId)
            .HasConversion(ValueObjectConverters.ProviderItemIdConverter)
            .HasMaxLength(128)
            .IsRequired();

        builder
            .Property(item => item.StoreExternalId)
            .HasConversion(ValueObjectConverters.StoreExternalIdConverter)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(item => item.Name).HasMaxLength(256).IsRequired();

        builder.Property(item => item.Description).HasColumnType("text");

        builder.Property(item => item.ThumbnailUrl).HasMaxLength(2048);

        builder.Property(item => item.PreviewUrl).HasMaxLength(2048);

        builder.Property(item => item.Type).HasMaxLength(64).IsRequired();

        builder.Property(item => item.CategoryName).HasMaxLength(200).IsRequired();

        builder
            .Property(item => item.UserInputDefinitions)
            .HasColumnType("jsonb")
            .HasConversion(new JsonListConverter<string>())
            .Metadata.SetValueComparer(JsonListComparer.Create<string>());

        builder
            .Property(item => item.Sources)
            .HasColumnType("jsonb")
            .HasConversion(new JsonListConverter<string>())
            .Metadata.SetValueComparer(JsonListComparer.Create<string>());

        // Stock is a complex type, not a jsonb blob: availability is the item grid's default filter
        // and it is expressed over Quantity.Current, so the predicate has to reach SQL.
        builder.ComplexProperty(
            item => item.Quantity,
            quantity =>
            {
                quantity.Property(value => value.Total).HasColumnName("QuantityTotal");
                quantity.Property(value => value.Current).HasColumnName("QuantityCurrent");
            }
        );

        builder
            .Property(item => item.Cooldown)
            .HasColumnType("jsonb")
            .HasConversion(ValueObjectConverters.ItemCooldownConverter);

        builder
            .Property(item => item.PreviousAvailability)
            .HasConversion(ValueObjectConverters.ItemAvailabilityConverter);

        builder
            .HasOne(item => item.WishlistEntry)
            .WithOne(entry => entry.StoreItem)
            .HasForeignKey<WishlistEntry>(entry => entry.StoreItemId);

        // Covers the category facet. Its sibling — the default availability view over
        // (WatchedStoreId, Enabled, QuantityCurrent) — cannot be declared here: EF cannot build an
        // index over a complex-type member, so that one is created as raw SQL by the
        // MakeItemQuantityQueryable migration and is deliberately absent from the model snapshot.
        builder.HasIndex(item => new { item.WatchedStoreId, item.CategoryName });

        builder.Ignore(item => item.Availability);
        builder.Ignore(item => item.DomainEvents);
    }
}
