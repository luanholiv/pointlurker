using System.Security.Claims;
using PointLurker.Core.Application.Identity;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Presentation.WebApi.Auth;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Errors;
using PointLurker.Presentation.WebApi.Search;
using PointLurker.Presentation.WebApi.Validation;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores;

public static class StoreEndpoints
{
    /// <summary>
    /// Chosen against a full store rather than against a screenful. No store observed holds more
    /// than ~500 items, so at 24 cards a full scroll would be 21 round trips; 48 halves that and
    /// costs nothing at this row size, while also shortening the window in which a concurrent sync
    /// can shift the result set under the reader.
    /// </summary>
    internal const int DefaultItemPageSize = 48;

    internal const int MaxItemPageSize = 200;

    public static IEndpointRouteBuilder MapStoreEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/stores").RequireAuthorization();

        group.MapPost("/", CreateStoreAsync).ValidatesBody<CreateStoreRequest>();
        group.MapGet("/", ListStoresAsync);
        group.MapGet("/summary", GetStoreSummaryAsync);
        group.MapGet("/{id:guid}", GetStoreAsync);
        group.MapGet("/{id:guid}/items", ListStoreItemsAsync);
        group.MapGet("/{id:guid}/items/facets", GetStoreItemFacetsAsync);
        group.MapDelete("/{id:guid}", DeleteStoreAsync);
        group.MapPatch("/{id:guid}/activate", ActivateAsync);
        group.MapPatch("/{id:guid}/deactivate", DeactivateAsync);
        group.MapGet("/{id:guid}/profile", GetStoreProfileAsync);
        group
            .MapPut("/{id:guid}/profile/values", UpsertStoreProfileValuesAsync)
            .ValidatesBody<UpsertStoreProfileValuesRequest>();
        group
            .MapPost("/{id:guid}/profile/aliases", UpsertStoreProfileAliasAsync)
            .ValidatesBody<UpsertStoreProfileAliasRequest>();
        // Labels are arbitrary streamer-authored strings ("Discord tag / handle", "Nick (IGN)").
        // They travel in the query string: one encoding rule, no catch-all route segment.
        group.MapDelete("/{id:guid}/profile/values", DeleteStoreProfileValueAsync);
        group.MapDelete("/{id:guid}/profile/aliases", DeleteStoreProfileAliasAsync);

        return app;
    }

    private static async Task<IResult> CreateStoreAsync(
        CreateStoreRequest request,
        ISingleUserProvider userProvider,
        IProviderCredentialProvider credentials,
        IStoreProviderClientFactory providers,
        AddWatchedStore addWatchedStore,
        TimeProvider timeProvider,
        CancellationToken ct
    )
    {
        // CreateStoreRequestValidator has already refused a slug that does not parse; an absent one
        // means StreamElements.
        var provider = StoreProvider.StreamElements;
        if (request.Provider is not null)
            StoreProviderSlugs.TryParse(request.Provider, out provider);

        // The credential check below needs the owner's id, so this read stays — and user.Id comes
        // from that same row rather than a second source.
        var user = await userProvider.GetAsync(ct);
        if (user is null)
        {
            return IdentityError.UserNotFound().ToHttpResult();
        }

        // Scoped to the provider being added: a GauPoints store never asks for the StreamElements
        // token, nor the reverse.
        if (await credentials.GetAsync(user.Id, provider, ct) is null)
        {
            return Results.Problem(
                ProblemDetailsFactory.Create(
                    new DomainError(
                        ErrorCodes.Auth.ProviderCredentialNotConfigured,
                        new Dictionary<string, object?> { ["provider"] = provider.ToSlug() }
                    ),
                    StatusCodes.Status403Forbidden,
                    $"No {provider} credential is configured. Set it in settings."
                )
            );
        }

        var client = providers.For(provider);
        StoreExternalId externalId;
        string displayName;
        string? avatarUrl = null;

        try
        {
            if (client.Capabilities.SupportsMultipleStores)
            {
                externalId = new StoreExternalId(request.ChannelId!);
                displayName = request.DisplayName!;
            }
            else
            {
                // One fixed store: its identity is the provider's to state, never the client's.
                var lookup = await client.LookupStoreAsync(string.Empty, ct);
                externalId = lookup.ExternalId;
                displayName = lookup.DisplayName;
                avatarUrl = lookup.AvatarUrl;
            }
        }
        catch (ProviderApiException)
        {
            return FetchFailed(provider);
        }

        IReadOnlyList<ProviderStoreItem> providerItems;
        try
        {
            providerItems = await client.GetStoreItemsAsync(externalId, ct);
        }
        catch (ProviderApiException)
        {
            return FetchFailed(provider);
        }

        var fetchedAt = timeProvider.GetUtcNow();
        var itemsData = providerItems.Select(item => item.ToApiData(fetchedAt)).ToArray();

        if (client.Capabilities.SupportsMultipleStores)
        {
            // Capture the channel avatar server-side. Best-effort: a lookup failure must not
            // block adding the store — the first sync will backfill the avatar.
            try
            {
                var lookup = await client.LookupStoreAsync(request.ChannelId!, ct);
                avatarUrl = lookup?.AvatarUrl;
            }
            catch (ProviderApiException)
            {
                avatarUrl = null;
            }
        }

        var result = await addWatchedStore.ExecuteAsync(
            user.Id,
            provider,
            externalId,
            displayName,
            itemsData,
            avatarUrl,
            ct
        );

        return result.Match(
            onSuccess: addResult =>
                Results.Created(
                    $"/api/stores/{addResult.Store.Id}",
                    (StoreDetailResponse)addResult
                ),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static IResult FetchFailed(StoreProvider provider) =>
        Results.Problem(
            ProblemDetailsFactory.Create(
                new DomainError(
                    ErrorCodes.ProviderApi.FetchFailed,
                    new Dictionary<string, object?> { ["provider"] = provider.ToSlug() }
                ),
                StatusCodes.Status502BadGateway,
                $"Failed to fetch the store from {provider}."
            )
        );

    private static async Task<IResult> GetStoreProfileAsync(
        Guid id,
        GetStoreInputProfile getStoreInputProfile,
        CancellationToken ct
    )
    {
        var result = await getStoreInputProfile.ExecuteAsync(id, ct);
        return result.Match(
            onSuccess: profile => Results.Ok((StoreProfileResponse)profile),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> UpsertStoreProfileValuesAsync(
        Guid id,
        UpsertStoreProfileValuesRequest request,
        UpsertStoreProfileValues upsertStoreProfileValues,
        CancellationToken ct
    )
    {
        var values = (request.Values ?? []).Select(v => (StoreProfileValueUpsert)v).ToArray();
        var result = await upsertStoreProfileValues.ExecuteAsync(id, values, ct);
        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> UpsertStoreProfileAliasAsync(
        Guid id,
        UpsertStoreProfileAliasRequest request,
        UpsertStoreProfileAlias upsertStoreProfileAlias,
        CancellationToken ct
    )
    {
        var result = await upsertStoreProfileAlias.ExecuteAsync(
            id,
            request.RawLabel,
            request.LabelCanonical,
            ct
        );
        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> DeleteStoreProfileValueAsync(
        Guid id,
        [AsParameters] StoreProfileValueDeleteQuery query,
        DeleteStoreProfileValue deleteStoreProfileValue,
        CancellationToken ct
    )
    {
        var result = await deleteStoreProfileValue.ExecuteAsync(
            id,
            query.Label ?? string.Empty,
            ct
        );
        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> DeleteStoreProfileAliasAsync(
        Guid id,
        [AsParameters] StoreProfileAliasDeleteQuery query,
        DeleteStoreProfileAlias deleteStoreProfileAlias,
        CancellationToken ct
    )
    {
        var result = await deleteStoreProfileAlias.ExecuteAsync(
            id,
            query.RawLabel ?? string.Empty,
            ct
        );
        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    /// <summary>
    /// Store volume does not need incremental loading, so the default slice is wide enough to serve
    /// the whole list in one request — but the endpoint still answers with the shared envelope, so
    /// a client reads it the same way it reads every other list.
    /// </summary>
    internal const int DefaultStorePageSize = 200;

    internal const int MaxStorePageSize = 500;

    internal static async Task<IResult> ListStoresAsync(
        ClaimsPrincipal principal,
        [AsParameters] StoreListQuery query,
        ListWatchedStores listWatchedStores,
        CancellationToken ct
    )
    {
        var search = new SearchQuery();

        var filter = new WatchedStoreFilter(
            TextContains: query.Q?.Trim(),
            IsActive: search.Flag(SearchParameters.Active, query.Active)
        );

        var slice = (
            Offset: SearchQuery.ClampOffset(search.Int32(SearchParameters.Offset, query.Offset)),
            Limit: SearchQuery.ClampLimit(
                search.Int32(SearchParameters.Limit, query.Limit),
                DefaultStorePageSize,
                MaxStorePageSize
            )
        );

        if (search.Failure is { } failure)
        {
            return failure;
        }

        var result = await listWatchedStores.ExecuteAsync(
            principal.GetUserId(),
            filter,
            slice.Offset,
            slice.Limit,
            ct
        );

        return result.Match(
            onSuccess: page => Results.Ok((StorePageResponse)page),
            onFailure: error => error.ToHttpResult()
        );
    }

    internal static async Task<IResult> GetStoreSummaryAsync(
        ClaimsPrincipal principal,
        GetWatchedStoreTotals getWatchedStoreTotals,
        CancellationToken ct = default
    )
    {
        var result = await getWatchedStoreTotals.ExecuteAsync(principal.GetUserId(), ct);

        return result.Match(
            onSuccess: totals => Results.Ok((StoreSummaryResponse)totals),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> GetStoreAsync(
        Guid id,
        GetStoreDetail getStoreDetail,
        CancellationToken ct
    )
    {
        var result = await getStoreDetail.ExecuteAsync(id, ct);

        return result.Match(
            onSuccess: store => Results.Ok((StoreDetailResponse)store),
            onFailure: error => error.ToHttpResult()
        );
    }

    internal static async Task<IResult> ListStoreItemsAsync(
        Guid id,
        [AsParameters] StoreItemsQuery query,
        ListStoreItems listStoreItems,
        CancellationToken ct
    )
    {
        var search = new SearchQuery();

        // Trimmed once, here: the surrounding spaces of a pasted term are not part of what the user
        // is looking for, and the matching arms downstream would each have to decide that again.
        var term = query.Q?.Trim();

        var filter = new StoreItemFilter(
            SearchTerm: term,
            // The grid opens on available items only, so an absent parameter means "on".
            AvailableOnly: search.Flag(SearchParameters.Available, query.Avail) ?? true,
            SubscriberOnly: search.Flag(SearchParameters.SubscriberOnly, query.Sub),
            Featured: search.Flag(SearchParameters.Featured, query.Feat),
            Categories: SearchQuery.Csv(query.Cat),
            MinCost: search.Int64(SearchParameters.MinCost, query.Min),
            MaxCost: search.Int64(SearchParameters.MaxCost, query.Max),
            // A searching caller who named no ordering wants the best matches, not the alphabet;
            // without a term there is nothing to rank, so the alphabet is the answer again. An
            // explicit `sort` still wins either way — this only chooses what "unset" means.
            SortField: search.EnumOr(
                SearchParameters.Sort,
                query.Sort,
                string.IsNullOrEmpty(term) ? StoreItemSortField.Name : StoreItemSortField.Relevance
            ),
            SortDirection: search.EnumOr(SearchParameters.Direction, query.Dir, SortDirection.Asc)
        );

        var slice = (
            Offset: SearchQuery.ClampOffset(search.Int32(SearchParameters.Offset, query.Offset)),
            Limit: SearchQuery.ClampLimit(
                search.Int32(SearchParameters.Limit, query.Limit),
                DefaultItemPageSize,
                MaxItemPageSize
            )
        );

        // Every parameter is read before anything is rejected, so the query runs only once the
        // whole request is known to be interpretable — and the caller is told about all of its bad
        // filters at once rather than one per round trip.
        if (search.Failure is { } failure)
        {
            return failure;
        }

        var result = await listStoreItems.ExecuteAsync(id, filter, slice.Offset, slice.Limit, ct);

        return result.Match(
            onSuccess: page => Results.Ok((StoreItemPageResponse)page),
            onFailure: error => error.ToHttpResult()
        );
    }

    internal static async Task<IResult> GetStoreItemFacetsAsync(
        Guid id,
        GetStoreItemFacets getStoreItemFacets,
        CancellationToken ct = default
    )
    {
        var result = await getStoreItemFacets.ExecuteAsync(id, ct);

        return result.Match(
            onSuccess: facets => Results.Ok((StoreItemFacetsResponse)facets),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> DeleteStoreAsync(
        Guid id,
        RemoveStore removeStore,
        CancellationToken ct
    )
    {
        var result = await removeStore.ExecuteAsync(id, ct);

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> ActivateAsync(
        Guid id,
        ActivateStore activateStore,
        CancellationToken ct
    )
    {
        var result = await activateStore.ExecuteAsync(id, ct);

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> DeactivateAsync(
        Guid id,
        DeactivateStore deactivateStore,
        CancellationToken ct
    )
    {
        var result = await deactivateStore.ExecuteAsync(id, ct);

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }
}
