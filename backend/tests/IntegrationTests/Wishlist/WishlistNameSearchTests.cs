using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using PointLurker.Infrastructure.Database;
using PointLurker.Infrastructure.Database.Repositories;

namespace IntegrationTests.Wishlist;

/// <summary>
/// How one term is allowed to find a wishlisted item. The wishlist matches the same name arms the
/// item grid does — word matching, substring and near-miss, all accent-folded — so a term that
/// finds an item from the store page finds it here too.
/// </summary>
/// <remarks>
/// The description is deliberately not searched: the wishlist does not render it, so a row found by
/// it would carry no visible reason for being there. Every item below shares one boilerplate
/// description, which is what the exclusion tests draw their terms from.
/// </remarks>
[Collection(ContainersCollection.Name)]
public sealed class WishlistNameSearchTests(DatabaseFixture fixture) : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private const string Boilerplate =
        "Estado: Field Tested -- FAVOR DEIXAR O INVENTARIO PUBLICO E NAO ALTERAR O TRADE LINK";

    private readonly Guid _userId = Guid.NewGuid();
    private Guid _storeId;

    public async Task InitializeAsync()
    {
        await fixture.ResetAsync();
        await SeedAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    #region Word matching

    [Theory]
    [InlineData("capillary stattrak")]
    [InlineData("stattrak capillary")]
    [InlineData("awp capillary")]
    public async Task Words_match_in_any_order(string term)
    {
        var names = await NamesAsync(term);

        names.Should().ContainSingle().Which.Should().Be("StatTrak™ AWP | Capillary");
    }

    [Theory]
    [InlineData("two times mccoy")]
    [InlineData("mccoy usaf")]
    [InlineData("tacp mccoy")]
    public async Task Words_separated_by_punctuation_match(string term)
    {
        var names = await NamesAsync(term);

        names.Should().ContainSingle().Which.Should().Be("'Two Times' McCoy | USAF TACP");
    }

    [Theory]
    [InlineData("doacao")]
    [InlineData("Doação")]
    [InlineData("doacao especial")]
    public async Task Accents_fold_both_ways(string term)
    {
        var names = await NamesAsync(term);

        names.Should().ContainSingle().Which.Should().Be("Doação Especial");
    }

    #endregion

    #region The arms word matching would otherwise cost

    [Theory]
    [InlineData("built")]
    [InlineData("keyb")]
    public async Task A_substring_inside_a_word_still_matches(string term)
    {
        var names = await NamesAsync(term);

        names.Should().ContainSingle().Which.Should().Be("Re.built Keyboard");
    }

    [Fact]
    public async Task A_single_word_near_miss_matches()
    {
        var names = await NamesAsync("Capilary");

        names.Should().ContainSingle().Which.Should().Be("StatTrak™ AWP | Capillary");
    }

    /// <summary>
    /// The words a multi-word term gets right would otherwise carry the score, so a term naming one
    /// item would return every similarly named sibling.
    /// </summary>
    [Fact]
    public async Task A_multi_word_near_miss_does_not_match()
    {
        var names = await NamesAsync("Doacao Especail");

        names.Should().BeEmpty();
    }

    [Theory]
    [InlineData("%")]
    [InlineData("_")]
    [InlineData("%%")]
    public async Task Wildcards_are_matched_literally(string term)
    {
        var names = await NamesAsync(term);

        names.Should().BeEmpty();
    }

    #endregion

    #region The description is not searched

    [Theory]
    [InlineData("field tested")]
    [InlineData("inventario")]
    [InlineData("nao alterar")]
    public async Task The_description_is_not_searched(string term)
    {
        var names = await NamesAsync(term);

        names.Should().BeEmpty();
    }

    #endregion

    #region Parity with the item grid

    /// <summary>
    /// No term here reaches the grid's description arm, which is the one deliberate difference;
    /// everything else has to agree.
    /// </summary>
    [Theory]
    [InlineData("capillary stattrak")]
    [InlineData("mccoy usaf")]
    [InlineData("doacao")]
    [InlineData("built")]
    [InlineData("Capilary")]
    [InlineData("keyb")]
    [InlineData("%")]
    [InlineData("nothing here")]
    public async Task The_wishlist_matches_a_name_term_the_way_the_item_grid_does(string term)
    {
        var fromWishlist = await ItemIdsAsync(term);
        var fromGrid = await GridItemIdsAsync(term);

        fromWishlist.Should().BeEquivalentTo(fromGrid);
    }

    #endregion

    private async Task<IReadOnlyList<string>> NamesAsync(string term)
    {
        var entries = await ListAsync(term);
        return [.. entries.Select(entry => entry.StoreItem!.Name)];
    }

    private async Task<IReadOnlyList<Guid>> ItemIdsAsync(string term)
    {
        var entries = await ListAsync(term);
        return [.. entries.Select(entry => entry.StoreItemId).Order()];
    }

    private async Task<IReadOnlyList<Guid>> GridItemIdsAsync(string term)
    {
        await using var scope = CreateScope();

        var (rows, _) = await new StoreItemRepository(scope.Db).ListAsync(
            _storeId,
            new StoreItemFilter(
                SearchTerm: term,
                AvailableOnly: false,
                SubscriberOnly: null,
                Featured: null,
                Categories: null,
                MinCost: null,
                MaxCost: null,
                SortField: StoreItemSortField.Name,
                SortDirection: SortDirection.Asc
            ),
            offset: 0,
            limit: 100
        );

        return [.. rows.Select(row => row.Id).Order()];
    }

    private async Task<IReadOnlyList<WishlistEntry>> ListAsync(string term)
    {
        await using var scope = CreateScope();

        return await new WishlistEntryRepository(scope.Db).ListByUserWithItemsAsync(
            _userId,
            new WishlistEntryFilter(
                SearchTerm: term,
                AvailableOnly: null,
                AutoRedeem: null,
                NotifyOnAvailable: null,
                BlockedOnInput: null,
                StoreId: null,
                SortField: WishlistEntrySortField.ItemName,
                SortDirection: SortDirection.Asc
            )
        );
    }

    private async Task SeedAsync()
    {
        await using var scope = CreateScope();

        _storeId = Guid.NewGuid();

        var store = new WatchedStore(
            _storeId,
            StoreProvider.StreamElements,
            new StoreExternalId("ch-names"),
            "Names",
            _userId,
            Now
        );

        string[] names =
        [
            "StatTrak™ AWP | Capillary",
            "'Two Times' McCoy | USAF TACP",
            "Doação Especial",
            "Re.built Keyboard",
        ];

        var items = names.Select(name => Item(_storeId, name)).ToArray();

        foreach (var item in items)
        {
            store.AddItem(item);
        }

        scope.Db.WatchedStores.Add(store);
        scope.Db.WishlistEntries.AddRange(
            items.Select(item => new WishlistEntry(Guid.NewGuid(), _userId, item.Id, Now))
        );

        await scope.Db.SaveChangesAsync();
    }

    private static StoreItem Item(Guid storeId, string name) =>
        new(
            Guid.NewGuid(),
            storeId,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId($"se-{Guid.NewGuid():N}"),
                new StoreExternalId("ch-names"),
                name,
                Boilerplate,
                "perk",
                100,
                "General",
                true,
                false,
                true,
                false,
                false,
                [],
                [],
                new ItemQuantity(10, 5),
                new ItemCooldown(0, 0, 0),
                Now,
                null,
                Now
            )
        );

    private DbScope CreateScope() => new(fixture.ConnectionString);

    private sealed class DbScope : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;

        public DbScope(string connectionString)
        {
            var services = new ServiceCollection();
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
            _provider = services.BuildServiceProvider();
            Db = _provider.GetRequiredService<AppDbContext>();
        }

        public AppDbContext Db { get; }

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }
}
