namespace PointLurker.Core.Application.Identity;

/// <summary>
/// Persists the owner's UI locale. The caller MUST validate the tag against
/// <c>SupportedLocales</c> first: <see cref="IdentityError"/> carries no params, and the
/// unsupported-locale response is a 400 with <c>{locale, allowed}</c> that only the transport layer
/// can assemble. An unvalidated tag still fails loudly rather than silently —
/// <c>ApplicationUser.SetLocale</c> rejects blanks, and the <c>CK_AspNetUsers_Locale_Allowlist</c>
/// CHECK constraint rejects anything off the allowlist.
/// </summary>
public sealed class SetUserLocale(ISingleUserProvider userProvider)
{
    public async Task<Result<Unit, IdentityError>> ExecuteAsync(
        string locale,
        CancellationToken ct = default
    )
    {
        var user = await userProvider.GetAsync(ct);
        if (user is null)
        {
            return Result<Unit, IdentityError>.Failure(IdentityError.UserNotFound());
        }

        user.SetLocale(locale);
        await userProvider.SaveAsync(user, ct);

        return Result<Unit, IdentityError>.Success(Unit.Value);
    }
}
