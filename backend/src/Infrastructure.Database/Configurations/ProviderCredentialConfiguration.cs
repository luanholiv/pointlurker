using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PointLurker.Core.Domain.Identity;

namespace PointLurker.Infrastructure.Database.Configurations;

public sealed class ProviderCredentialConfiguration : IEntityTypeConfiguration<ProviderCredential>
{
    public void Configure(EntityTypeBuilder<ProviderCredential> builder)
    {
        builder.ToTable("ProviderCredentials");

        // The key is the uniqueness rule: one credential per provider per user.
        builder.HasKey(credential => new { credential.UserId, credential.Provider });

        // int, append-only: see StoreProvider.
        builder.Property(credential => credential.Provider).HasConversion<int>();

        builder.Property(credential => credential.EncryptedSecret).HasMaxLength(4096).IsRequired();

        builder
            .Property(credential => credential.LastValidationError)
            .HasMaxLength(ProviderCredential.LastValidationErrorMaxLength);

        builder
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(credential => credential.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
