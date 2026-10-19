using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Identity;
using PointLurker.Core.Domain.Localization;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder
            .Property(user => user.Locale)
            .HasMaxLength(16)
            .IsRequired()
            .HasDefaultValue(SupportedLocales.EnUs);
    }
}
