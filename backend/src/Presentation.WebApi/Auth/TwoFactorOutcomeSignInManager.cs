using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PointLurker.Core.Domain.Identity;

namespace PointLurker.Presentation.WebApi.Auth;

/// <summary>
/// Records that a second factor was offered and refused, so <see cref="IdentityErrorContract" /> can
/// name that failure instead of collapsing it into a wrong password.
/// <para>
/// The two are the same document at the response — a <c>401</c> whose <c>detail</c> is
/// <c>SignInResult.ToString()</c>, which reads <c>Failed</c> either way — and the middleware sees
/// nothing else. The discriminator the response lacks is not in the request either: a
/// <c>twoFactorCode</c> in the body does not mean the code is what failed, because the login
/// endpoint attempts a second factor only once the password has been accepted. Submit a wrong
/// password alongside a perfectly good code and the attempt dies at the password, with the code
/// still sitting in the body.
/// </para>
/// <para>
/// So the question is asked of the component that knows the answer. Reaching this override means the
/// password was already accepted, which makes the outcome unambiguous by construction.
/// </para>
/// </summary>
public sealed class TwoFactorOutcomeSignInManager(
    UserManager<ApplicationUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<ApplicationUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<ApplicationUser> confirmation
)
    : SignInManager<ApplicationUser>(
        userManager,
        contextAccessor,
        claimsFactory,
        optionsAccessor,
        logger,
        schemes,
        confirmation
    )
{
    /// <summary>
    /// Set on <c>HttpContext.Items</c> for the lifetime of the sign-in request. The middleware runs
    /// around endpoint execution, so it reads this after the endpoint has written its response.
    /// <para>
    /// Deliberately not shaped like an error code: this is a per-request note between two
    /// components, never a value that reaches a response, and the presentation-layer convention test
    /// rejects a bare <c>namespace.snake_case</c> literal that no <c>ErrorCodes</c> constant backs.
    /// </para>
    /// </summary>
    internal const string SecondFactorRejectedKey = "PointLurker.SecondFactorRejected";

    public override async Task<SignInResult> TwoFactorAuthenticatorSignInAsync(
        string code,
        bool isPersistent,
        bool rememberClient
    )
    {
        var result = await base.TwoFactorAuthenticatorSignInAsync(
            code,
            isPersistent,
            rememberClient
        );

        // A lockout or a not-allowed account keeps sharing a code with a plain failure, deliberately:
        // those say something about the account rather than about the digits that were typed.
        if (!result.Succeeded && !result.IsLockedOut && !result.IsNotAllowed)
        {
            Context.Items[SecondFactorRejectedKey] = true;
        }

        return result;
    }
}
