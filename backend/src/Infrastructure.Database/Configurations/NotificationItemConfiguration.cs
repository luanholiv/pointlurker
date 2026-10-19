using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class NotificationItemConfiguration : IEntityTypeConfiguration<NotificationItem>
{
    public void Configure(EntityTypeBuilder<NotificationItem> builder)
    {
        builder.ToTable("notification_items");
        builder.HasKey(item => item.Id);

        builder.Property(item => item.StoreName).HasMaxLength(200).IsRequired();

        builder.Property(item => item.ItemName).HasMaxLength(256).IsRequired();
    }
}
