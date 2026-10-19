using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Identity;
using PointLurker.Presentation.WebApi.Endpoints.Auth.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Auth;

/// <summary>
/// Authentication is served by the framework Identity API (<c>MapIdentityApi</c>). The only
/// custom endpoint left under <c>/api/auth</c> is the anonymous first-run gate, which is
/// pre-session and therefore stays on the auth namespace. The current-user resource lives under
/// <c>/api/users/me</c> (see <see cref="Users.UserEndpoints"/>).
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth");
        group.MapGet("/bootstrap", GetBootstrapStateAsync);
        return app;
    }

    internal static async Task<IResult> GetBootstrapStateAsync(
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken
    )
    {
        var userExists = await userManager.Users.AnyAsync(cancellationToken);
        return Results.Ok(new BootstrapResponse(userExists));
    }
}
