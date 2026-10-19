using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(notification => notification.Id);

        builder
            .Property(notification => notification.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder
            .Property(notification => notification.Channel)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(notification => notification.FailureReason).HasMaxLength(2000);

        builder
            .HasMany(notification => notification.Items)
            .WithOne()
            .HasForeignKey(item => item.NotificationId);
    }
}
