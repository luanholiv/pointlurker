using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Localization;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;
using PointLurker.Presentation.WebApi.Errors;
using PointLurker.Presentation.WebApi.Validation;

namespace PointLurker.Presentation.WebApi.Endpoints.Users;

/// <summary>
/// Current-user resource, namespaced by concern under <c>/api/users/me</c>. Authentication-based
/// authorization only (no roles): any authenticated user may act on their own resource.
/// </summary>
public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").RequireAuthorization();
        group.MapGet("/me", GetCurrentUserAsync);
        group.MapPatch("/me/locale", SetLocaleAsync).ValidatesBody<SetLocaleRequest>();
        group
            .MapPut("/me/credentials/{provider}", SetCredentialAsync)
            .ValidatesBody<SetCredentialRequest>();
        group.MapDelete("/me/credentials/{provider}", ClearCredentialAsync);
        return app;
    }

    internal static async Task<IResult> GetCurrentUserAsync(
        ISingleUserProvider userProvider,
        IProviderCredentialProvider credentials,
        CancellationToken ct
    )
    {
        var user = await userProvider.GetAsync(ct);
        if (user is null)
        {
            return IdentityError.UserNotFound().ToHttpResult();
        }

        var held = await credentials.ListAsync(user.Id, ct);

        return Results.Ok((CurrentUserResponse)new CurrentUser(user, held));
    }

    internal static async Task<IResult> SetLocaleAsync(
        SetLocaleRequest request,
        SetUserLocale setUserLocale,
        CancellationToken ct
    )
    {
        if (!SupportedLocales.IsSupported(request.Locale))
        {
            return Results.Problem(
                ProblemDetailsFactory.Create(
                    new DomainError(
                        ErrorCodes.Users.LocaleUnsupported,
                        new Dictionary<string, object?>
                        {
                            ["locale"] = request.Locale,
                            ["allowed"] = SupportedLocales.All,
                        }
                    ),
                    StatusCodes.Status400BadRequest,
                    $"Locale '{request.Locale}' is not supported."
                )
            );
        }

        var result = await setUserLocale.ExecuteAsync(request.Locale, ct);

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    internal static async Task<IResult> SetCredentialAsync(
        [AsParameters] CredentialProviderRoute route,
        SetCredentialRequest request,
        SetProviderCredential setProviderCredential,
        CancellationToken ct
    )
    {
        if (!StoreProviderSlugs.TryParse(route.Provider, out var provider))
        {
            return IdentityError.CredentialProviderUnsupported(route.Provider ?? "").ToHttpResult();
        }

        var result = await setProviderCredential.ExecuteAsync(provider, request.Secret, ct);

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    internal static async Task<IResult> ClearCredentialAsync(
        [AsParameters] CredentialProviderRoute route,
        ClearProviderCredential clearProviderCredential,
        CancellationToken ct
    )
    {
        if (!StoreProviderSlugs.TryParse(route.Provider, out var provider))
        {
            return IdentityError.CredentialProviderUnsupported(route.Provider ?? "").ToHttpResult();
        }

        var result = await clearProviderCredential.ExecuteAsync(provider, ct);

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }
}
