using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Stores;

/// <summary>
/// Narrowing, slicing and faceting a store's items, all of it decided by the database. Everything
/// here runs against real PostgreSQL because that is the only place the translation can be wrong:
/// an untranslatable predicate does not fail, it silently evaluates in memory.
/// </summary>
[Collection(ContainersCollection.Name)]
public sealed class StoreItemSearchTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly Guid _storeId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _emptyStoreId = Guid.NewGuid();
    private readonly Guid _textStoreId = Guid.NewGuid();
    private Guid _wishlistedItemId;
    private Guid _wishlistEntryId;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Filters

    [Fact]
    public async Task Name_search_matches_a_case_insensitive_substring()
    {
        var (rows, total) = await ListAsync(Filter() with { SearchTerm = "emo" });

        rows.Should()
            .OnlyContain(row => row.Name.Contains("Emo", StringComparison.OrdinalIgnoreCase));
        rows.Select(row => row.Name).Should().Contain(["Emote Pack", "emoji bundle"]);
        total.Should().Be(rows.Count);
    }

    [Fact]
    public async Task A_wildcard_in_the_search_term_is_matched_literally()
    {
        var (rows, _) = await ListAsync(Filter() with { SearchTerm = "%" });

        rows.Select(row => row.Name).Should().Equal("100% Cotton Shirt");
    }

    [Fact]
    public async Task An_underscore_in_the_search_term_is_matched_literally()
    {
        var (rows, _) = await ListAsync(Filter() with { SearchTerm = "_" });

        rows.Select(row => row.Name).Should().Equal("under_score");
    }

    [Fact]
    public async Task Availability_narrows_to_available_items()
    {
        var (rows, _) = await ListAsync(Filter() with { AvailableOnly = true });

        rows.Should().OnlyContain(row => row.IsAvailable);
        rows.Should().NotContain(row => row.Name == "Sold Out");
        rows.Should().NotContain(row => row.Name == "Disabled Item");
    }

    /// <summary>
    /// Pins what every row <i>reports</i>, not just which rows come back.
    ///
    /// The suite otherwise only ever asserts <c>IsAvailable</c> where it is true — through
    /// <c>AvailableOnly</c>, which narrows in SQL — so a projection that answered <c>true</c>
    /// unconditionally would satisfy all of it. These are the unavailable cases, which only appear
    /// at all once the filter is off.
    /// </summary>
    [Fact]
    public async Task Every_row_reports_the_domain_availability_rule()
    {
        var (rows, _) = await ListAsync(Filter() with { AvailableOnly = false });

        // Not a dictionary: the seed deliberately repeats "Tied Item" seven times.
        bool Available(string name) => rows.First(row => row.Name == name).IsAvailable;

        // Enabled, but nothing left.
        Available("Sold Out").Should().BeFalse();
        // In stock, but switched off.
        Available("Disabled Item").Should().BeFalse();
        // -1 is unlimited, which MUST read as available.
        Available("Unlimited Badge").Should().BeTrue();
        Available("Emote Pack").Should().BeTrue();
    }

    [Fact]
    public async Task Unlimited_stock_counts_as_available()
    {
        var (rows, _) = await ListAsync(Filter() with { AvailableOnly = true });

        rows.Should().Contain(row => row.Name == "Unlimited Badge" && row.QuantityCurrent == -1);
    }

    [Fact]
    public async Task Availability_can_be_turned_off()
    {
        var (_, withFilter) = await ListAsync(Filter() with { AvailableOnly = true });
        var (_, withoutFilter) = await ListAsync(Filter() with { AvailableOnly = false });

        withoutFilter.Should().BeGreaterThan(withFilter);
    }

    [Fact]
    public async Task Subscriber_only_narrows_the_result()
    {
        var (rows, _) = await ListAsync(Filter() with { SubscriberOnly = true });

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(row => row.SubscriberOnly);
    }

    [Fact]
    public async Task Featured_narrows_the_result()
    {
        var (rows, _) = await ListAsync(Filter() with { Featured = true });

        rows.Select(row => row.Name).Should().Equal("Featured Cape");
    }

    [Fact]
    public async Task Categories_narrow_to_the_listed_set()
    {
        var (rows, _) = await ListAsync(Filter() with { Categories = ["Emotes", "Badges"] });

        rows.Should().NotBeEmpty();
        rows.Should()
            .OnlyContain(row => row.CategoryName == "Emotes" || row.CategoryName == "Badges");
    }

    [Fact]
    public async Task Cost_bounds_are_inclusive()
    {
        var (rows, _) = await ListAsync(Filter() with { MinCost = 100, MaxCost = 200 });

        rows.Should().OnlyContain(row => row.Cost >= 100 && row.Cost <= 200);
        rows.Select(row => row.Cost).Should().Contain([100, 200]);
    }

    [Fact]
    public async Task Filters_combine_conjunctively()
    {
        var (rows, _) = await ListAsync(Filter() with { SearchTerm = "cape", Featured = true });

        rows.Select(row => row.Name).Should().Equal("Featured Cape");
    }

    [Fact]
    public async Task A_conjunction_that_matches_nothing_returns_an_empty_result_with_a_zero_total()
    {
        var (rows, total) = await ListAsync(
            Filter() with
            {
                SearchTerm = "cape",
                SubscriberOnly = true,
            }
        );

        rows.Should().BeEmpty();
        total.Should().Be(0);
    }

    #endregion

    #region Slicing

    [Fact]
    public async Task Successive_slices_tile_the_result_set()
    {
        var (_, total) = await ListAsync(Filter());

        var seen = new List<Guid>();
        for (var offset = 0; offset < total; offset += 4)
        {
            var (page, reported) = await ListAsync(Filter(), offset, 4);
            reported.Should().Be(total);
            seen.AddRange(page.Select(row => row.Id));
        }

        seen.Should().HaveCount(total);
        seen.Should().OnlyHaveUniqueItems();
    }

    /// <summary>
    /// Every item here shares one sort key, so nothing but the <c>Id</c> tiebreaker decides the
    /// order across slice boundaries. Without it PostgreSQL is free to return rows in a different
    /// order per query, which duplicates some and drops others while looking like a UI glitch.
    /// </summary>
    [Fact]
    public async Task Paging_over_one_shared_sort_key_loses_and_repeats_nothing()
    {
        var (_, total) = await ListAsync(Filter() with { Categories = ["Tied"] });
        total.Should().BeGreaterThan(4, "the fixture must span several slices to be meaningful");

        var seen = new List<Guid>();
        for (var offset = 0; offset < total; offset += 2)
        {
            var (page, _) = await ListAsync(Filter() with { Categories = ["Tied"] }, offset, 2);
            seen.AddRange(page.Select(row => row.Id));
        }

        seen.Should().HaveCount(total);
        seen.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task An_offset_past_the_end_returns_nothing_but_still_reports_the_total()
    {
        var (_, total) = await ListAsync(Filter());

        var (rows, reported) = await ListAsync(Filter(), offset: total + 50, limit: 10);

        rows.Should().BeEmpty();
        reported.Should().Be(total);
    }

    [Fact]
    public async Task The_total_counts_matching_rows_not_returned_rows()
    {
        var (rows, total) = await ListAsync(Filter(), offset: 0, limit: 2);

        rows.Should().HaveCount(2);
        total.Should().BeGreaterThan(2);
    }

    #endregion

    #region Row completeness

    [Fact]
    public async Task A_row_carries_every_field_the_grid_renders()
    {
        var (rows, _) = await ListAsync(Filter() with { SearchTerm = "Emote Pack" });

        var row = rows.Should().ContainSingle().Subject;
        row.Id.Should().NotBeEmpty();
        row.ProviderItemId.Should().NotBeNullOrEmpty();
        row.Name.Should().Be("Emote Pack");
        row.Description.Should().Be("A pack of emotes");
        row.Cost.Should().Be(100);
        row.CategoryName.Should().Be("Emotes");
        row.SubscriberOnly.Should().BeFalse();
        row.IsAvailable.Should().BeTrue();
        row.QuantityCurrent.Should().Be(5);
        row.QuantityTotal.Should().Be(10);
        row.AllowMessages.Should().BeTrue();
        row.ThumbnailUrl.Should().Be("https://cdn.example.com/emote.png");
        row.ProviderCreatedAt.Should().NotBe(default);
    }

    [Fact]
    public async Task A_wishlisted_item_carries_its_entry_identifier()
    {
        var (rows, _) = await ListAsync(Filter());

        rows.Single(row => row.Id == _wishlistedItemId)
            .WishlistEntryId.Should()
            .Be(_wishlistEntryId);
    }

    [Fact]
    public async Task A_non_wishlisted_item_reports_no_entry()
    {
        var (rows, _) = await ListAsync(Filter());

        rows.Where(row => row.Id != _wishlistedItemId)
            .Should()
            .OnlyContain(row => row.WishlistEntryId == null);
    }

    /// <summary>
    /// The wishlist identifier is a navigation property, so a projected query does not inherit the
    /// <c>ThenInclude</c> the store-detail read relies on. If it were resolved per row instead of
    /// joined, this would be one query per card — which is what the statement count catches.
    /// </summary>
    [Fact]
    public async Task Wishlist_state_is_resolved_by_the_same_query_as_the_items()
    {
        var statements = new List<string>();
        await using var scope = CreateScope(statements);

        var (rows, _) = await new StoreItemRepository(scope.Db).ListAsync(
            _storeId,
            Filter(),
            offset: 0,
            limit: 100
        );

        rows.Should().HaveCountGreaterThan(1);
        // One statement for the total, one for the slice. Anything more is a per-row lookup.
        statements.Should().HaveCount(2);
        statements
            .Should()
            .Contain(sql => sql.Contains("wishlist_entries", StringComparison.Ordinal));
    }

    #endregion

    #region Facets

    [Fact]
    public async Task Facets_report_every_category_in_the_store()
    {
        var facets = await FacetsAsync();

        facets.Categories.Should().Contain(["Emotes", "Badges", "Apparel", "Tied"]);
    }

    [Fact]
    public async Task Facets_report_the_stores_cost_bounds()
    {
        await using var scope = CreateScope();
        var costs = await scope
            .Db.StoreItems.Where(item => item.WatchedStoreId == _storeId)
            .Select(item => item.Cost)
            .ToListAsync();

        var facets = await FacetsAsync();

        facets.MinCost.Should().Be(costs.Min());
        facets.MaxCost.Should().Be(costs.Max());
    }

    [Fact]
    public async Task Facets_are_unchanged_by_a_category_filter()
    {
        var unfiltered = await FacetsAsync();

        var (_, narrowedTotal) = await ListAsync(Filter() with { Categories = ["Emotes"] });
        var (_, wholeStore) = await ListAsync(Filter());
        narrowedTotal.Should().BeLessThan(wholeStore, "the filter must actually narrow something");

        var afterFiltering = await FacetsAsync();

        afterFiltering.Categories.Should().Equal(unfiltered.Categories);
        afterFiltering
            .Categories.Should()
            .Contain("Badges", "a category nothing in the filter mentions");
    }

    [Fact]
    public async Task Facets_are_unchanged_by_a_cost_filter()
    {
        var unfiltered = await FacetsAsync();

        await ListAsync(Filter() with { MinCost = 100, MaxCost = 200 });
        var afterFiltering = await FacetsAsync();

        afterFiltering.MinCost.Should().Be(unfiltered.MinCost);
        afterFiltering.MaxCost.Should().Be(unfiltered.MaxCost);
    }

    [Fact]
    public async Task Facet_categories_are_reported_in_a_stable_order()
    {
        var first = await FacetsAsync();
        var second = await FacetsAsync();

        second.Categories.Should().Equal(first.Categories);
    }

    [Fact]
    public async Task A_store_with_no_items_reports_empty_facets()
    {
        await using var scope = CreateScope();

        var facets = await new StoreItemRepository(scope.Db).GetFacetsAsync(_emptyStoreId);

        facets.Categories.Should().BeEmpty();
        facets.MinCost.Should().Be(0);
        facets.MaxCost.Should().Be(0);
    }

    #endregion

    #region Full-text matching

    /// <summary>
    /// The two words appear in the other order, and one of them is hyphenated to its neighbour.
    /// A substring test reads "tested field" and "Field-Tested" as unrelated strings and finds
    /// neither in the other, which is the whole reason matching is tokenized now.
    /// </summary>
    [Fact]
    public async Task Words_match_in_any_order_and_across_a_hyphen()
    {
        var (rows, _) = await ListTextAsync("tested field");

        rows.Select(row => row.Name).Should().Equal("AWP | Redline");
    }

    [Fact]
    public async Task An_unaccented_term_matches_accented_text()
    {
        var (rows, _) = await ListTextAsync("doacao");

        rows.Select(row => row.Name).Should().Contain("Doação de Gaupoints");
    }

    /// <summary>
    /// The reverse direction, and the one the data forces: this description spells the word without
    /// its accent, so a reader who types it correctly still has to find the row.
    /// </summary>
    [Fact]
    public async Task An_accented_term_matches_unaccented_text()
    {
        var (rows, _) = await ListTextAsync("inventário");

        rows.Select(row => row.Name).Should().Equal("AWP | Redline");
    }

    [Fact]
    public async Task A_term_only_in_the_description_matches()
    {
        var (rows, _) = await ListTextAsync("publico");

        rows.Select(row => row.Name).Should().Equal("AWP | Redline");
    }

    /// <summary>
    /// The parser reads "Re.built" as a single lexeme, so word matching alone cannot see "built"
    /// inside it. This is the case that would have regressed against the old substring search.
    /// </summary>
    [Fact]
    public async Task A_term_inside_a_single_lexeme_is_still_found()
    {
        var (rows, _) = await ListTextAsync("built");

        rows.Select(row => row.Name).Should().Equal("P250 | Re.built");
    }

    /// <summary>
    /// Same shape, digits: "240Hz" is one lexeme, so "240" is only reachable as a substring.
    /// </summary>
    [Fact]
    public async Task A_number_joined_to_a_word_is_still_found()
    {
        var (rows, _) = await ListTextAsync("240");

        rows.Select(row => row.Name).Should().Equal("Monitor Gamer 240Hz");
    }

    [Fact]
    public async Task A_misspelled_term_matches()
    {
        var (rows, _) = await ListTextAsync("redlien");

        rows.Select(row => row.Name).Should().Contain("AWP | Redline");
    }

    /// <summary>
    /// A term of several words names one item, so it has to return one item. Guessing at a near
    /// miss across a phrase scores it by the words it gets right — every "Bulk Item NNN" is a 0.857
    /// match for every other — so the arm that catches a misspelling has to stay out of this.
    /// </summary>
    [Fact]
    public async Task A_multi_word_term_does_not_match_its_siblings()
    {
        var (rows, _) = await ListTextAsync("Bulk Item 001");

        rows.Select(row => row.Name).Should().Equal("Bulk Item 001");
    }

    /// <summary>
    /// The single-word half of the same rule: one word is still guessed at, so the arm is off for
    /// phrases rather than off altogether.
    /// </summary>
    [Fact]
    public async Task A_single_word_term_still_matches_a_misspelling()
    {
        var (rows, _) = await ListTextAsync("Redlin");

        rows.Select(row => row.Name).Should().Contain("AWP | Redline");
    }

    /// <summary>
    /// A term this short is meant literally. Matching it approximately would sweep in rows that
    /// merely share a letter with it, which is what the length gate on the near-miss arm prevents.
    /// </summary>
    [Fact]
    public async Task A_single_character_term_is_matched_exactly()
    {
        // A letter no name carries, folded or not — "ç" becomes "c", so that one would have hit.
        var (rows, _) = await ListTextAsync("q");

        rows.Should().BeEmpty();
    }

    /// <summary>
    /// The term names one item and appears in another's description. The name is the text the grid
    /// renders, so that row comes first however the scores fall.
    /// </summary>
    [Fact]
    public async Task A_name_match_outranks_a_description_match()
    {
        var (rows, _) = await ListTextAsync("gaupoints");

        rows.Select(row => row.Name).Should().Equal("Doação de Gaupoints", "VIP");
    }

    /// <summary>
    /// Relevance is best-first in both directions: the reverse of "closest answers" is not
    /// something a caller asks for, so the direction is ignored rather than honoured.
    /// </summary>
    [Fact]
    public async Task Relevance_ignores_the_sort_direction()
    {
        var ascending = await ListTextAsync("gaupoints");
        var descending = await ListTextAsync("gaupoints", SortDirection.Desc);

        descending
            .Rows.Select(row => row.Name)
            .Should()
            .Equal(ascending.Rows.Select(row => row.Name));
    }

    /// <summary>
    /// A relevance sort with nothing to rank is not an error — the enum accepts the value — so it
    /// answers with the ordering it would have used anyway.
    /// </summary>
    [Fact]
    public async Task Relevance_without_a_term_orders_by_name()
    {
        var byRelevance = await ListTextAsync(null, sortField: StoreItemSortField.Relevance);
        var byName = await ListTextAsync(null, sortField: StoreItemSortField.Name);

        byRelevance
            .Rows.Select(row => row.Name)
            .Should()
            .Equal(byName.Rows.Select(row => row.Name));
    }

    #endregion

    private static StoreItemFilter Filter() =>
        new(
            SearchTerm: null,
            AvailableOnly: false,
            SubscriberOnly: null,
            Featured: null,
            Categories: null,
            MinCost: null,
            MaxCost: null,
            SortField: StoreItemSortField.Name,
            SortDirection: SortDirection.Asc
        );

    private async Task<(IReadOnlyList<StoreItemListRow> Rows, int Total)> ListAsync(
        StoreItemFilter filter,
        int offset = 0,
        int limit = 100
    )
    {
        await using var scope = CreateScope();
        return await new StoreItemRepository(scope.Db).ListAsync(_storeId, filter, offset, limit);
    }

    /// <summary>
    /// Lists the text fixture's own store, ordered by relevance unless told otherwise. It is a
    /// second store rather than more rows in the first so that the counts, tiles and golden orders
    /// the tests above measure are not disturbed by fixture data they never asked for.
    /// </summary>
    private async Task<(IReadOnlyList<StoreItemListRow> Rows, int Total)> ListTextAsync(
        string? term,
        SortDirection direction = SortDirection.Asc,
        StoreItemSortField sortField = StoreItemSortField.Relevance
    )
    {
        var filter = Filter() with
        {
            SearchTerm = term,
            SortField = sortField,
            SortDirection = direction,
        };

        await using var scope = CreateScope();
        return await new StoreItemRepository(scope.Db).ListAsync(_textStoreId, filter, 0, 100);
    }

    private async Task<StoreItemFacets> FacetsAsync()
    {
        await using var scope = CreateScope();
        return await new StoreItemRepository(scope.Db).GetFacetsAsync(_storeId);
    }

    private async Task SeedAsync()
    {
        await using var scope = CreateScope();

        var store = new WatchedStore(
            _storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-search"),
            "Search",
            _userId,
            Now
        );
        store.AddItem(
            Item(
                "Emote Pack",
                100,
                "Emotes",
                current: 5,
                description: "A pack of emotes",
                allowMessages: true,
                thumbnailUrl: "https://cdn.example.com/emote.png"
            )
        );
        store.AddItem(Item("emoji bundle", 200, "Emotes", current: 2));
        store.AddItem(Item("Unlimited Badge", 50, "Badges", current: -1));
        store.AddItem(Item("Sold Out", 300, "Badges", current: 0));
        store.AddItem(Item("Disabled Item", 400, "Badges", current: 9, enabled: false));
        store.AddItem(Item("Featured Cape", 500, "Apparel", current: 4, featured: true));
        store.AddItem(Item("Sub Hoodie", 600, "Apparel", current: 4, subscriberOnly: true));
        store.AddItem(Item("100% Cotton Shirt", 700, "Apparel", current: 4));
        store.AddItem(Item("under_score", 800, "Apparel", current: 4));

        // One shared name, cost and category, so nothing but the Id tiebreaker separates them.
        for (var i = 0; i < 7; i++)
        {
            store.AddItem(Item("Tied Item", 999, "Tied", current: 1));
        }

        var wishlisted = store.Items.First(item => item.Name == "Emote Pack");
        _wishlistedItemId = wishlisted.Id;
        _wishlistEntryId = Guid.NewGuid();

        var emptyStore = new WatchedStore(
            _emptyStoreId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-empty"),
            "Empty",
            _userId,
            Now
        );

        // Shaped after a real catalogue rather than invented: names in English, descriptions in
        // Portuguese, the same word spelled with and without its accent across rows, and two names
        // the text parser reads as a single lexeme. Each row exists for a case the old substring
        // search or plain word matching gets wrong on its own.
        var textStore = new WatchedStore(
            _textStoreId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-text"),
            "Text",
            _userId,
            Now
        );

        StoreItem TextItem(string name, long cost, string? description = null) =>
            Item(
                name,
                cost,
                "Skins",
                current: 3,
                description: description,
                storeId: _textStoreId,
                channel: "ch-text"
            );

        textStore.AddItem(
            TextItem(
                "AWP | Redline",
                900,
                "Estado: Field-Tested -- FAVOR DEIXAR O INVENTARIO PUBLICO"
            )
        );
        textStore.AddItem(TextItem("Doação de Gaupoints", 1000, "Você estará doando pontos"));
        textStore.AddItem(TextItem("VIP", 1100, "Converte Gaupoints em vantagens"));
        textStore.AddItem(TextItem("P250 | Re.built", 1200));
        textStore.AddItem(TextItem("Monitor Gamer 240Hz", 1300));

        // Sequentially named siblings, which is where a near-miss guess does the most damage: every
        // one of these is a near miss for every other, differing only in the digits.
        for (var i = 1; i <= 3; i++)
        {
            textStore.AddItem(TextItem($"Bulk Item {i:D3}", 1400 + i));
        }

        scope.Db.WatchedStores.Add(store);
        scope.Db.WatchedStores.Add(emptyStore);
        scope.Db.WatchedStores.Add(textStore);
        scope.Db.WishlistEntries.Add(
            new WishlistEntry(_wishlistEntryId, _userId, _wishlistedItemId, Now)
        );
        await scope.Db.SaveChangesAsync();
    }

    private int _seed;

    private StoreItem Item(
        string name,
        long cost,
        string category,
        int current,
        bool enabled = true,
        bool featured = false,
        bool subscriberOnly = false,
        bool allowMessages = false,
        string? description = null,
        string? thumbnailUrl = null,
        Guid? storeId = null,
        string channel = "ch-search"
    )
    {
        var index = ++_seed;
        return new StoreItem(
            Guid.NewGuid(),
            storeId ?? _storeId,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId($"se-search-{index}"),
                new StoreExternalId(channel),
                name,
                description,
                "perk",
                cost,
                category,
                enabled,
                featured,
                true,
                subscriberOnly,
                allowMessages,
                [],
                [],
                new ItemQuantity(10, current),
                new ItemCooldown(0, 0, 0),
                Now.AddMinutes(index),
                null,
                Now,
                thumbnailUrl
            )
        );
    }

    private DbScope CreateScope(List<string>? statements = null) =>
        new(fixture.ConnectionString, statements);

    private sealed class DbScope : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public DbScope(string connectionString, List<string>? statements)
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(connectionString);
                if (statements is not null)
                {
                    options.LogTo(
                        statements.Add,
                        [DbLoggerCategory.Database.Command.Name],
                        LogLevel.Information
                    );
                }
            });
            _provider = services.BuildServiceProvider();
            Db = _provider.GetRequiredService<AppDbContext>();
        }

        public AppDbContext Db { get; }

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }
}
