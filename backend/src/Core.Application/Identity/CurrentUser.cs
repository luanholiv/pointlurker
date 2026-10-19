using PointLurker.Core.Domain.Identity;

namespace PointLurker.Core.Application.Identity;

/// <summary>The owner together with the provider credentials they hold.</summary>
public sealed record CurrentUser(
    ApplicationUser User,
    IReadOnlyList<ProviderCredential> Credentials
);
