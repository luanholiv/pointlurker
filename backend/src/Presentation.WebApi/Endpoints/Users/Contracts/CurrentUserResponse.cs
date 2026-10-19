using PointLurker.Core.Application.Identity;

namespace PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

/// <param name="Credentials">One summary per stored credential, ordered by provider.</param>
internal sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    string Locale,
    bool TwoFactorEnabled,
    IReadOnlyList<CredentialSummaryResponse> Credentials
)
{
    public static explicit operator CurrentUserResponse(CurrentUser current) =>
        new(
            current.User.Id,
            current.User.Email ?? string.Empty,
            current.User.Locale,
            current.User.TwoFactorEnabled,
            current.Credentials.Select(credential => (CredentialSummaryResponse)credential).ToList()
        );
}
