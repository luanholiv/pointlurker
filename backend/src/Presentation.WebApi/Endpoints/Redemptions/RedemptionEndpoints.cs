using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Redemptions.Contracts;
using PointLurker.Presentation.WebApi.Search;

namespace PointLurker.Presentation.WebApi.Endpoints.Redemptions;

public static class RedemptionEndpoints
{
    internal const int DefaultPageSize = 20;
    internal const int MaxPageSize = 100;

    public static IEndpointRouteBuilder MapRedemptionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/redemptions").RequireAuthorization();

        group.MapGet("/", ListRedemptionsAsync);

        return app;
    }

    internal static async Task<IResult> ListRedemptionsAsync(
        [AsParameters] RedemptionListQuery query,
        ListRedemptions listRedemptions,
        CancellationToken ct
    )
    {
        var search = new SearchQuery();

        var filter = new RedemptionFilter(
            Status: search.OptionalEnum<RedemptionStatus>(SearchParameters.Status, query.Status),
            WishlistEntryId: search.Identifier(
                SearchParameters.WishlistEntry,
                query.WishlistEntryId
            ),
            From: search.Timestamp(SearchParameters.From, query.From),
            To: search.Timestamp(SearchParameters.To, query.To),
            // Most recent first: the history's default view is "what happened lately".
            SortField: search.EnumOr(
                SearchParameters.Sort,
                query.Sort,
                RedemptionSortField.AttemptedAt
            ),
            SortDirection: search.EnumOr(SearchParameters.Direction, query.Dir, SortDirection.Desc)
        );

        var slice = (
            Offset: SearchQuery.ClampOffset(search.Int32(SearchParameters.Offset, query.Offset)),
            Limit: SearchQuery.ClampLimit(
                search.Int32(SearchParameters.Limit, query.Limit),
                DefaultPageSize,
                MaxPageSize
            )
        );

        if (search.Failure is { } failure)
        {
            return failure;
        }

        var result = await listRedemptions.ExecuteAsync(filter, slice.Offset, slice.Limit, ct);

        return Results.Ok((RedemptionPageResponse)result);
    }
}
