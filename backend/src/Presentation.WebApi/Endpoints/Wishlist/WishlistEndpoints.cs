using System.Security.Claims;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Presentation.WebApi.Auth;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;
using PointLurker.Presentation.WebApi.Errors;
using PointLurker.Presentation.WebApi.Search;
using PointLurker.Presentation.WebApi.Validation;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist;

public static class WishlistEndpoints
{
    public static IEndpointRouteBuilder MapWishlistEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/wishlist").RequireAuthorization();

        group.MapPost("/", CreateWishlistEntryAsync).ValidatesBody<CreateWishlistEntryRequest>();
        group.MapGet("/", ListWishlistEntriesAsync);
        group.MapGet("/{id:guid}", GetWishlistEntryByIdAsync);
        group
            .MapPut("/{id:guid}", UpdateWishlistEntryByIdAsync)
            .ValidatesBody<UpdateWishlistEntryRequest>();
        group.MapDelete("/{id:guid}", DeleteWishlistEntryAsync);

        return app;
    }

    private static async Task<IResult> CreateWishlistEntryAsync(
        CreateWishlistEntryRequest request,
        ClaimsPrincipal principal,
        AddWishlistEntry addWishlistEntry,
        CancellationToken ct
    )
    {
        var result = await addWishlistEntry.ExecuteAsync(
            principal.GetUserId(),
            request.StoreItemId,
            request.NotifyOnAvailable,
            request.AutoRedeem,
            request.DesiredRedemptions,
            request.RedemptionMessage,
            ct
        );

        return result.Match(
            onSuccess: success =>
                Results.Created(
                    $"/api/wishlist/{success.Entry.Id}",
                    (CreateWishlistEntryResponse)success
                ),
            onFailure: error => error.ToHttpResult()
        );
    }

    internal static async Task<IResult> ListWishlistEntriesAsync(
        ClaimsPrincipal principal,
        [AsParameters] WishlistListQuery query,
        ListWishlistEntriesGrouped listWishlistEntriesGrouped,
        CancellationToken ct
    )
    {
        var search = new SearchQuery();

        var term = query.Q?.Trim();

        var filter = new WishlistEntryFilter(
            SearchTerm: term,
            AvailableOnly: search.Flag(SearchParameters.Available, query.Avail),
            AutoRedeem: search.Flag(SearchParameters.AutoRedeem, query.Auto),
            NotifyOnAvailable: search.Flag(SearchParameters.Notify, query.Notify),
            BlockedOnInput: search.Flag(SearchParameters.Blocked, query.Blocked),
            StoreId: search.Identifier(SearchParameters.Store, query.Store),
            SortField: search.EnumOr(
                SearchParameters.Sort,
                query.Sort,
                WishlistEntrySortField.ItemName
            ),
            SortDirection: search.EnumOr(SearchParameters.Direction, query.Dir, SortDirection.Asc)
        );

        if (search.Failure is { } failure)
        {
            return failure;
        }

        var result = await listWishlistEntriesGrouped.ExecuteAsync(
            principal.GetUserId(),
            filter,
            ct
        );

        return result.Match(
            onSuccess: grouped => Results.Ok((WishlistPageResponse)grouped),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> GetWishlistEntryByIdAsync(
        Guid id,
        GetWishlistEntry getWishlistEntry,
        CancellationToken ct
    )
    {
        var result = await getWishlistEntry.ExecuteAsync(id, ct);

        return result.Match(
            onSuccess: entry => Results.Ok((WishlistEntryDetailResponse)entry),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> UpdateWishlistEntryByIdAsync(
        Guid id,
        UpdateWishlistEntryRequest request,
        UpdateWishlistEntry updateWishlistEntry,
        CancellationToken ct
    )
    {
        var result = await updateWishlistEntry.ExecuteAsync(
            id,
            request.NotifyOnAvailable,
            request.AutoRedeem,
            request.DesiredRedemptions,
            request.RedemptionMessage,
            ct
        );

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }

    private static async Task<IResult> DeleteWishlistEntryAsync(
        Guid id,
        RemoveWishlistEntry removeWishlistEntry,
        CancellationToken ct
    )
    {
        var result = await removeWishlistEntry.ExecuteAsync(id, ct);

        return result.Match(
            onSuccess: _ => Results.NoContent(),
            onFailure: error => error.ToHttpResult()
        );
    }
}
