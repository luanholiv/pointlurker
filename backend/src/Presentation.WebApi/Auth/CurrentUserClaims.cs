using System.Security.Claims;

namespace PointLurker.Presentation.WebApi.Auth;

/// <summary>
/// Reads the owner's id straight off the bearer principal, so handlers that only need "who is
/// asking" cost zero database round-trips. Every route reaching this sits behind a group-level
/// <c>RequireAuthorization()</c>, so an anonymous caller is rejected by the authorization
/// middleware long before a handler runs. A principal arriving here without a parsable
/// <see cref="ClaimTypes.NameIdentifier"/> means the bearer scheme or the claims principal factory
/// is misconfigured — a server fault no token refresh could fix — so it throws (surfacing as a 500)
/// rather than returning 401, which the frontend would answer with a refresh and a spurious logout.
/// See docs/AUTH.md.
/// </summary>
internal static class CurrentUserClaims
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(raw, out var id)
            ? id
            : throw new InvalidOperationException(
                "The authenticated principal carries no parsable NameIdentifier claim."
            );
    }
}
