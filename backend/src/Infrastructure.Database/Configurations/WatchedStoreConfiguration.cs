using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;
using PointLurker.Infrastructure.Database.ValueConverters;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class WatchedStoreConfiguration : IEntityTypeConfiguration<WatchedStore>
{
    public void Configure(EntityTypeBuilder<WatchedStore> builder)
    {
        builder.ToTable("watched_stores");
        builder.HasKey(store => store.Id);

        // int, append-only: see StoreProvider.
        builder.Property(store => store.Provider).HasConversion<int>().IsRequired();

        builder
            .Property(store => store.ExternalId)
            .HasConversion(ValueObjectConverters.StoreExternalIdConverter)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(store => store.DisplayName).HasMaxLength(200).IsRequired();

        builder.Property(store => store.AvatarUrl).HasMaxLength(2048);

        builder.Property(store => store.IsActive).HasDefaultValue(true);

        builder.Property(store => store.CreatedAt).IsRequired();

        builder
            .HasIndex(store => new
            {
                store.UserId,
                store.Provider,
                store.ExternalId,
            })
            .IsUnique();

        builder
            .HasMany(store => store.Items)
            .WithOne(item => item.WatchedStore)
            .HasForeignKey(item => item.WatchedStoreId);

        builder
            .Property(store => store.InputValues)
            .HasField("_inputValues")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("input_values")
            .HasColumnType("jsonb")
            .HasDefaultValue(Array.Empty<StoreInputValue>())
            .HasConversion(new JsonListConverter<StoreInputValue>())
            .Metadata.SetValueComparer(JsonListComparer.Create<StoreInputValue>());

        builder
            .Property(store => store.InputAliases)
            .HasField("_inputAliases")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("input_aliases")
            .HasColumnType("jsonb")
            .HasDefaultValue(Array.Empty<StoreInputAlias>())
            .HasConversion(new JsonListConverter<StoreInputAlias>())
            .Metadata.SetValueComparer(JsonListComparer.Create<StoreInputAlias>());
    }
}
