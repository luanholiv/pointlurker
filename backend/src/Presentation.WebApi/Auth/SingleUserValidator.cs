using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Identity;

namespace PointLurker.Presentation.WebApi.Auth;

/// <summary>
/// Enforces the single-user invariant at the ASP.NET Identity core: creation fails once any
/// other user account exists. Runs inside <see cref="UserManager{TUser}.CreateAsync(TUser)"/>
/// and <c>UpdateAsync</c>, so it guards the framework <c>register</c> endpoint (and any future
/// caller) without an endpoint-specific guard.
/// </summary>
public sealed class SingleUserValidator : IUserValidator<ApplicationUser>
{
    /// <summary>Identity error code raised when a second account is attempted.</summary>
    public const string SingleUserErrorCode = "SingleUserOnly";

    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user
    )
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(user);

        // Exclude the user being validated by Id: on create, the new user's Id differs from the
        // existing row so the count is >= 1 and creation is blocked; on update of the sole user,
        // that row is self and is not counted, so the update is allowed.
        var others = await manager.Users.CountAsync(u => u.Id != user.Id);

        return others >= 1
            ? IdentityResult.Failed(
                new IdentityError
                {
                    Code = SingleUserErrorCode,
                    Description = "Only a single user account may exist.",
                }
            )
            : IdentityResult.Success;
    }
}
