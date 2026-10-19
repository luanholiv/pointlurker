using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Configuration;
using PointLurker.Core.Domain.Identity;
using PointLurker.Infrastructure.Database;

namespace PointLurker.Presentation.WebApi.Auth;

public static class AuthServiceCollectionExtensions
{
    public static IServiceCollection AddAppAuthentication(this IServiceCollection services)
    {
        // Single-user app: authenticated == authorized. No role-based policy; protected endpoints use
        // RequireAuthorization(). Roles are not registered at all — no RoleManager, no role validators.
        // The AspNetRoles/AspNetUserRoles tables stay mapped by IdentityDbContext; existing rows are
        // inert (no migration).
        services.AddAuthorization();

        // Lockout and password rules come from IdentityPolicyOptions below, not a setup delegate:
        // the delegate has no access to DI, so it could only ever hold literals.
        // The sign-in manager is replaced to record one thing the login response cannot express: that
        // a second factor was offered and refused. See TwoFactorOutcomeSignInManager.
        services
            .AddIdentityApiEndpoints<ApplicationUser>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager<TwoFactorOutcomeSignInManager>();

        // Enforce the single-user invariant at the Identity core so it holds for every user-creation
        // path (including the framework `register` endpoint), not just one endpoint. Registered
        // after AddIdentityApiEndpoints so it runs behind the framework's own user validators.
        services.AddScoped<IUserValidator<ApplicationUser>, SingleUserValidator>();

        // Configurable, startup-validated app-session token lifetimes applied to the Bearer scheme.
        services.AddValidatedOptions<AuthTokenOptions, AuthTokenOptionsValidator>(
            AuthTokenOptions.SectionName
        );

        // Same story for the account policy. PostConfigure so it lands after every framework setup
        // step, including the one AddIdentityApiEndpoints registers.
        services.AddValidatedOptions<IdentityPolicyOptions, IdentityPolicyOptionsValidator>(
            IdentityPolicyOptions.SectionName
        );

        services
            .AddOptions<IdentityOptions>()
            .PostConfigure<IOptions<IdentityPolicyOptions>>(
                (identity, policyOptions) =>
                {
                    var policy = policyOptions.Value;
                    identity.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(
                        policy.LockoutDurationMinutes
                    );
                    identity.Lockout.MaxFailedAccessAttempts = policy.LockoutMaxFailedAttempts;
                    identity.Password.RequiredLength = policy.PasswordMinimumLength;
                }
            );

        services
            .AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme)
            .PostConfigure<IOptions<AuthTokenOptions>>(
                (bearer, authOptions) =>
                {
                    var auth = authOptions.Value;
                    bearer.BearerTokenExpiration = TimeSpan.FromMinutes(
                        auth.AccessTokenExpirationMinutes
                    );
                    bearer.RefreshTokenExpiration = TimeSpan.FromHours(
                        auth.RefreshTokenExpirationHours
                    );
                }
            );

        return services;
    }
}
