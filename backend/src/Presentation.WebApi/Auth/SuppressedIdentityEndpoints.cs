namespace PointLurker.Presentation.WebApi.Auth;

/// <summary>
/// Suppresses the <c>MapIdentityApi</c> endpoints we do not expose. <c>MapIdentityApi</c> has no
/// per-endpoint opt-out, so this runs as terminal middleware <em>before</em> endpoint parameter
/// binding — an endpoint filter is too late, because some suppressed endpoints (e.g.
/// <c>resetPassword</c>) throw during body binding before a filter would run. Returns
/// <c>404 Not Found</c> for:
/// <list type="bullet">
///   <item>the email-confirmation / password-reset endpoints (any method): <c>forgotPassword</c>,
///   <c>resetPassword</c>, <c>confirmEmail</c>, <c>resendConfirmationEmail</c>; and</item>
///   <item><c>GET manage/info</c> only — <c>POST manage/info</c> (email/password change) stays
///   reachable, so the method branch is required.</item>
/// </list>
/// </summary>
public static class SuppressedIdentityEndpoints
{
    private static readonly string[] AlwaysSuppressedSuffixes =
    [
        "/forgotPassword",
        "/resetPassword",
        "/confirmEmail",
        "/resendConfirmationEmail",
    ];

    public static IApplicationBuilder UseSuppressedIdentityEndpoints(this IApplicationBuilder app)
    {
        return app.Use(
            async (context, next) =>
            {
                if (ShouldSuppress(context.Request))
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                await next();
            }
        );
    }

    private static bool ShouldSuppress(HttpRequest request)
    {
        var path = request.Path.Value ?? string.Empty;

        foreach (var suffix in AlwaysSuppressedSuffixes)
        {
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return HttpMethods.IsGet(request.Method)
            && path.EndsWith("/manage/info", StringComparison.OrdinalIgnoreCase);
    }
}
