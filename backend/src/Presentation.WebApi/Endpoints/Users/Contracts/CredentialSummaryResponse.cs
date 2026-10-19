using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Identity;

namespace PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

/// <summary>
/// What the owner may know about one stored credential. Never the secret, nor anything read from
/// it beyond the account name and expiry the settings page shows.
/// </summary>
/// <param name="Provider">The provider slug.</param>
/// <param name="LastValidationError">An error code only, never provider text.</param>
internal sealed record CredentialSummaryResponse(
    string Provider,
    string? AccountName,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationError
)
{
    public static explicit operator CredentialSummaryResponse(ProviderCredential credential) =>
        new(
            credential.Provider.ToSlug(),
            credential.AccountName,
            credential.ExpiresAt,
            credential.LastValidatedAt,
            credential.LastValidationError
        );
}
