using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Errors;
using PointLurker.Presentation.WebApi.Validation;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores;

/// <summary>
/// Resolves the store a provider would watch, before it is added. One endpoint for every provider,
/// so the add dialog has one code path: StreamElements searches by channel username, GauPoints has
/// one fixed store and reads its catalogue to prove it is reachable.
/// </summary>
public static class StoreLookupEndpoints
{
    public static IEndpointRouteBuilder MapStoreLookupEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/stores/lookup", LookupStoreAsync)
            .RequireAuthorization()
            .ValidatesQuery<StoreLookupQuery>();
        return app;
    }

    internal static async Task<IResult> LookupStoreAsync(
        [AsParameters] StoreLookupQuery query,
        IStoreProviderClientFactory providers,
        CancellationToken ct
    )
    {
        // StoreLookupQueryValidator has already refused a provider that does not parse, and a blank
        // query where one is needed.
        StoreProviderSlugs.TryParse(query.Provider, out var provider);
        var slug = provider.ToSlug();
        var trimmedQuery = query.Query?.Trim() ?? string.Empty;

        try
        {
            var lookup = await providers.For(provider).LookupStoreAsync(trimmedQuery, ct);
            return Results.Ok((StoreLookupResponse)lookup);
        }
        catch (ProviderApiException ex) when (ex.StatusCode == StatusCodes.Status404NotFound)
        {
            return Results.Problem(
                ProblemDetailsFactory.Create(
                    new DomainError(
                        ErrorCodes.StoreLookup.NotFound,
                        new Dictionary<string, object?>
                        {
                            ["provider"] = slug,
                            ["query"] = trimmedQuery,
                        }
                    ),
                    StatusCodes.Status404NotFound,
                    $"No {provider} store found for '{trimmedQuery}'."
                )
            );
        }
        catch (ProviderApiException ex) when (ex.IsUnreachable)
        {
            return Results.Problem(
                ProblemDetailsFactory.Create(
                    new DomainError(
                        ErrorCodes.ProviderApi.Unreachable,
                        new Dictionary<string, object?> { ["provider"] = slug }
                    ),
                    StatusCodes.Status502BadGateway,
                    $"{provider} is unreachable. Try again later."
                )
            );
        }
        catch (ProviderApiException)
        {
            return Results.Problem(
                ProblemDetailsFactory.Create(
                    new DomainError(
                        ErrorCodes.ProviderApi.FetchFailed,
                        new Dictionary<string, object?> { ["provider"] = slug }
                    ),
                    StatusCodes.Status502BadGateway,
                    $"{provider} answered with an error."
                )
            );
        }
    }
}
