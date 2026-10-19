using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Presentation.WebApi.Endpoints.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;
using PointLurker.Presentation.WebApi.Search;

namespace Presentation.WebApi.UnitTests.Endpoints.Stores;

/// <summary>
/// The store-items endpoint reads the same query-string names the frontend already writes to the
/// browser URL, so its existing serializer becomes the API query string untranslated. These assert
/// that mapping, the slice bounds, and that an unknown store is a 404 rather than an empty page.
/// </summary>
public sealed class StoreItemEndpointsTests
{
    private readonly IStoreItemRepository _items = Substitute.For<IStoreItemRepository>();
    private readonly IWatchedStoreRepository _stores = Substitute.For<IWatchedStoreRepository>();
    private readonly ListStoreItems _listStoreItems;
    private readonly GetStoreItemFacets _getFacets;
    private readonly Guid _storeId = Guid.NewGuid();

    public StoreItemEndpointsTests()
    {
        _listStoreItems = new ListStoreItems(_stores, _items);
        _getFacets = new GetStoreItemFacets(_stores, _items);
        _stores.ExistsAsync(_storeId, Arg.Any<CancellationToken>()).Returns(true);
        _items
            .ListAsync(
                Arg.Any<Guid>(),
                Arg.Any<StoreItemFilter>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(((IReadOnlyList<StoreItemListRow>)[], 0));
        _items
            .GetFacetsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new StoreItemFacets([], 0, 0));
    }

    [Fact]
    public async Task Returns_200_for_an_existing_store()
    {
        var result = await ListAsync();

        StatusOf(result).Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Returns_404_for_an_unknown_store()
    {
        var unknown = Guid.NewGuid();
        _stores.ExistsAsync(unknown, Arg.Any<CancellationToken>()).Returns(false);

        var result = await StoreEndpoints.ListStoreItemsAsync(
            unknown,
            Query(),
            _listStoreItems,
            CancellationToken.None
        );

        StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
        ProblemOf(result).Extensions["code"].Should().Be(ErrorCodes.Stores.NotFound);
    }

    [Fact]
    public async Task Facets_return_404_for_an_unknown_store()
    {
        var unknown = Guid.NewGuid();
        _stores.ExistsAsync(unknown, Arg.Any<CancellationToken>()).Returns(false);

        var result = await StoreEndpoints.GetStoreItemFacetsAsync(unknown, _getFacets);

        StatusOf(result).Should().Be(StatusCodes.Status404NotFound);
        ProblemOf(result).Extensions["code"].Should().Be(ErrorCodes.Stores.NotFound);
    }

    #region Parameter names

    [Fact]
    public async Task The_frontends_parameter_names_are_understood_untranslated()
    {
        await ListAsync(
            q: "gold",
            avail: "0",
            sub: "1",
            feat: "1",
            cat: "Emotes,Badges",
            min: "100",
            max: "200",
            sort: "cost",
            dir: "desc"
        );

        await _items
            .Received(1)
            .ListAsync(
                _storeId,
                Arg.Is<StoreItemFilter>(filter =>
                    filter.SearchTerm == "gold"
                    && !filter.AvailableOnly
                    && filter.SubscriberOnly == true
                    && filter.Featured == true
                    && filter.Categories!.Count == 2
                    && filter.MinCost == 100
                    && filter.MaxCost == 200
                    && filter.SortField == StoreItemSortField.Cost
                    && filter.SortDirection == SortDirection.Desc
                ),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Availability_defaults_to_on_when_the_parameter_is_absent()
    {
        await ListAsync();

        await _items
            .Received(1)
            .ListAsync(
                _storeId,
                Arg.Is<StoreItemFilter>(filter => filter.AvailableOnly),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task Sorting_defaults_to_name_ascending()
    {
        await ListAsync();

        await _items
            .Received(1)
            .ListAsync(
                _storeId,
                Arg.Is<StoreItemFilter>(filter =>
                    filter.SortField == StoreItemSortField.Name
                    && filter.SortDirection == SortDirection.Asc
                ),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task An_unknown_sort_field_is_rejected_before_the_query_runs()
    {
        var result = await ListAsync(sort: "bogus");

        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);
        var fieldError = FirstFieldError(result);
        fieldError["code"].Should().Be(ErrorCodes.Search.InvalidEnumValue);
        fieldError["field"].Should().Be(SearchParameters.Sort);

        await _items
            .DidNotReceive()
            .ListAsync(
                Arg.Any<Guid>(),
                Arg.Any<StoreItemFilter>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task A_malformed_cost_bound_is_rejected()
    {
        var result = await ListAsync(min: "abc");

        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);
        FirstFieldError(result)["code"].Should().Be(ErrorCodes.Search.InvalidFilterValue);
    }

    /// <summary>
    /// Two typos are reported together, in the order the handler read them — <c>min</c> is built
    /// into the filter before <c>sort</c> is. Naming only the first would make the caller fix them
    /// one round trip at a time.
    /// </summary>
    [Fact]
    public async Task Every_uninterpretable_filter_is_reported_at_once()
    {
        var result = await ListAsync(sort: "bogus", min: "abc");

        StatusOf(result).Should().Be(StatusCodes.Status400BadRequest);

        var problem = ProblemOf(result);
        var wrapper = (IReadOnlyDictionary<string, object?>)problem.Extensions["params"]!;
        var entries = ((IEnumerable<object?>)wrapper["errors"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .ToList();

        entries
            .Select(entry => (string?)entry["field"])
            .Should()
            .Equal(SearchParameters.MinCost, SearchParameters.Sort);
    }

    #endregion

    #region Slice bounds

    [Fact]
    public async Task An_oversized_limit_is_clamped()
    {
        await ListAsync(limit: "99999");

        await _items
            .Received(1)
            .ListAsync(
                _storeId,
                Arg.Any<StoreItemFilter>(),
                Arg.Is(0),
                Arg.Is(StoreEndpoints.MaxItemPageSize),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task An_absent_limit_takes_the_documented_default()
    {
        await ListAsync();

        await _items
            .Received(1)
            .ListAsync(
                _storeId,
                Arg.Any<StoreItemFilter>(),
                Arg.Is(0),
                Arg.Is(StoreEndpoints.DefaultItemPageSize),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task A_negative_offset_starts_at_the_beginning()
    {
        await ListAsync(offset: "-5");

        await _items
            .Received(1)
            .ListAsync(
                _storeId,
                Arg.Any<StoreItemFilter>(),
                Arg.Is(0),
                Arg.Is(StoreEndpoints.DefaultItemPageSize),
                Arg.Any<CancellationToken>()
            );
    }

    [Fact]
    public async Task The_response_echoes_the_slice_bounds_that_produced_it()
    {
        var result = await ListAsync(offset: "20", limit: "10");

        var page = ValueOf<StoreItemPageResponse>(result);
        page.Offset.Should().Be(20);
        page.Limit.Should().Be(10);
    }

    [Fact]
    public async Task The_total_is_reported_independently_of_the_returned_slice()
    {
        _items
            .ListAsync(
                Arg.Any<Guid>(),
                Arg.Any<StoreItemFilter>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>()
            )
            .Returns(((IReadOnlyList<StoreItemListRow>)[Row("A"), Row("B")], 57));

        var page = ValueOf<StoreItemPageResponse>(await ListAsync(limit: "2"));

        page.Items.Should().HaveCount(2);
        page.TotalCount.Should().Be(57);
    }

    [Fact]
    public async Task An_empty_result_reports_a_total_of_zero()
    {
        var page = ValueOf<StoreItemPageResponse>(await ListAsync());

        page.Items.Should().BeEmpty();
        page.TotalCount.Should().Be(0);
    }

    #endregion

    #region Facets

    [Fact]
    public async Task Facets_report_the_stores_categories_and_cost_bounds()
    {
        _items
            .GetFacetsAsync(_storeId, Arg.Any<CancellationToken>())
            .Returns(new StoreItemFacets(["Badges", "Emotes"], 9, 1000));

        var result = await StoreEndpoints.GetStoreItemFacetsAsync(_storeId, _getFacets);

        var facets = ValueOf<StoreItemFacetsResponse>(result);
        facets.Categories.Should().Equal("Badges", "Emotes");
        facets.MinCost.Should().Be(9);
        facets.MaxCost.Should().Be(1000);
    }

    [Fact]
    public async Task Facets_are_read_without_any_filter()
    {
        await StoreEndpoints.GetStoreItemFacetsAsync(_storeId, _getFacets);

        // The repository's facet method takes no filter at all — the type system is what keeps a
        // future edit from narrowing the category list to whatever the user has already picked.
        await _items.Received(1).GetFacetsAsync(_storeId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_item_page_carries_no_facet_fields()
    {
        var page = ValueOf<StoreItemPageResponse>(await ListAsync());

        typeof(StoreItemPageResponse)
            .GetProperties()
            .Select(property => property.Name)
            .Should()
            .BeEquivalentTo("Items", "TotalCount", "Offset", "Limit");
        page.Should().NotBeNull();
    }

    #endregion

    private Task<IResult> ListAsync(
        string? q = null,
        string? avail = null,
        string? sub = null,
        string? feat = null,
        string? cat = null,
        string? min = null,
        string? max = null,
        string? sort = null,
        string? dir = null,
        string? offset = null,
        string? limit = null
    ) =>
        StoreEndpoints.ListStoreItemsAsync(
            _storeId,
            Query(q, avail, sub, feat, cat, min, max, sort, dir, offset, limit),
            _listStoreItems,
            CancellationToken.None
        );

    private static StoreItemsQuery Query(
        string? q = null,
        string? avail = null,
        string? sub = null,
        string? feat = null,
        string? cat = null,
        string? min = null,
        string? max = null,
        string? sort = null,
        string? dir = null,
        string? offset = null,
        string? limit = null
    ) => new(q, avail, sub, feat, cat, min, max, sort, dir, offset, limit);

    /// <summary>
    /// A rejected filter answers in the shared <c>validation.failed</c> envelope, so the code naming
    /// what actually broke is the one on the first <c>params.errors</c> entry.
    /// </summary>
    private static IReadOnlyDictionary<string, object?> FirstFieldError(IResult result)
    {
        var problem = ProblemOf(result);
        problem.Extensions["code"].Should().Be(ErrorCodes.Validation.Failed);

        var wrapper = (IReadOnlyDictionary<string, object?>)problem.Extensions["params"]!;
        return ((IEnumerable<object?>)wrapper["errors"]!)
            .Cast<IReadOnlyDictionary<string, object?>>()
            .First();
    }

    private static StoreItemListRow Row(string name) =>
        new(
            Guid.NewGuid(),
            "se-1",
            name,
            null,
            100,
            "General",
            false,
            true,
            5,
            10,
            false,
            null,
            null,
            DateTimeOffset.UnixEpoch
        );

    private static int StatusOf(IResult result) =>
        ((IStatusCodeHttpResult)result).StatusCode!.Value;

    private static ProblemDetails ProblemOf(IResult result) =>
        (ProblemDetails)result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;

    private static T ValueOf<T>(IResult result) =>
        (T)result.Should().BeAssignableTo<IValueHttpResult>().Subject.Value!;
}
