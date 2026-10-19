using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class GetStoreInputProfileTests
{
    private readonly IWatchedStoreRepository _storeRepository =
        Substitute.For<IWatchedStoreRepository>();
    private readonly FuzzyInputLabelResolver _resolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);
    private readonly GetStoreInputProfile _sut;

    public GetStoreInputProfileTests()
    {
        _sut = new GetStoreInputProfile(_storeRepository, _resolver);
    }

    [Fact]
    public async Task Groups_every_requested_raw_label_under_the_value_that_satisfies_it()
    {
        var store = CreateStore();
        var ts = Now;
        store.UpsertValue("Twitch Username", "ct", ts);
        store.UpsertAlias("twitchh usrname", "Twitch Username");
        AddItem(store, ["Twitch Username"]);
        AddItem(store, ["twitchh usrname"]);
        AddItem(store, ["Username Twitch"]);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        result.IsSuccess.Should().BeTrue();
        result.Value.Unresolved.Should().BeEmpty();
        var slot = result.Value.Resolved.Should().ContainSingle().Subject;
        slot.LabelCanonical.Should().Be("Twitch Username");
        slot.RequestedBy.Select(r => r.RawLabel)
            .Should()
            .BeEquivalentTo(["Twitch Username", "twitchh usrname", "Username Twitch"]);
        slot.RequestedBy.Select(r => r.Source)
            .Should()
            .Equal(ResolutionSource.Exact, ResolutionSource.Alias, ResolutionSource.Fuzzy);
    }

    [Fact]
    public async Task Reports_a_value_no_item_requests_as_resolved_with_no_requesters()
    {
        var store = CreateStore();
        store.UpsertValue("Steam ID", "ct", Now);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        var slot = result.Value.Resolved.Should().ContainSingle().Subject;
        slot.LabelCanonical.Should().Be("Steam ID");
        slot.RequestedBy.Should().BeEmpty();
    }

    [Fact]
    public async Task Deduplicates_a_raw_label_requested_by_several_items_case_insensitively()
    {
        var store = CreateStore();
        AddItem(store, ["Discord Tag", "  discord   tag "]);
        AddItem(store, ["DISCORD TAG"]);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        var entry = result.Value.Unresolved.Should().ContainSingle().Subject;
        entry.RawLabel.Should().Be("Discord Tag");
        entry.ItemCount.Should().Be(2);
    }

    [Fact]
    public async Task Ignores_blank_input_definitions()
    {
        var store = CreateStore();
        AddItem(store, ["", "   "]);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        result.Value.Unresolved.Should().BeEmpty();
        result.Value.Resolved.Should().BeEmpty();
    }

    [Fact]
    public async Task Alias_pointing_at_a_canonical_without_a_value_is_unresolved_and_names_the_target()
    {
        var store = CreateStore();
        store.UpsertAlias("Steam profile", "Steam ID");
        AddItem(store, ["Steam profile"]);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        result.Value.Resolved.Should().BeEmpty();
        var entry = result.Value.Unresolved.Should().ContainSingle().Subject;
        entry.RawLabel.Should().Be("Steam profile");
        entry.AliasTarget.Should().Be("Steam ID");
    }

    [Fact]
    public async Task Reports_item_count_and_blocked_entry_count_for_unresolved_labels()
    {
        var store = CreateStore();
        AddItem(store, ["Nome no jogo"], blockedOnInput: true);
        AddItem(store, ["Nome no jogo"], blockedOnInput: true);
        AddItem(store, ["Nome no jogo"]);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        var entry = result.Value.Unresolved.Should().ContainSingle().Subject;
        entry.ItemCount.Should().Be(3);
        entry.BlockedEntryCount.Should().Be(2);
    }

    [Fact]
    public async Task Orders_unresolved_by_blocked_then_item_count_then_label()
    {
        var store = CreateStore();
        // "Alpha" and "Beta" tie on both counts, so the label breaks the tie.
        AddItem(store, ["Beta", "Alpha", "Busy label"]);
        AddItem(store, ["Popular label"]);
        AddItem(store, ["Popular label"]);
        AddItem(store, ["Busy label"], blockedOnInput: true);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        result
            .Value.Unresolved.Select(u => u.RawLabel)
            .Should()
            .Equal("Busy label", "Popular label", "Alpha", "Beta");
    }

    [Fact]
    public async Task Returns_not_found_when_store_missing()
    {
        _storeRepository
            .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((WatchedStore?)null);

        var result = await _sut.ExecuteAsync(Guid.NewGuid());

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.Stores.NotFound);
    }

    [Fact]
    public async Task Exposes_stored_labels_for_the_map_to_existing_picker()
    {
        var store = CreateStore();
        var ts = Now;
        store.UpsertValue("Twitch Username", "ct", ts);
        store.UpsertValue("Steam ID", "ct2", ts);
        Returns(store);

        var result = await _sut.ExecuteAsync(store.Id);

        result.Value.CanonicalLabels.Should().Equal("Twitch Username", "Steam ID");
    }

    private void Returns(WatchedStore store) =>
        _storeRepository.GetByIdAsync(store.Id, Arg.Any<CancellationToken>()).Returns(store);

    private static WatchedStore CreateStore() =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "Test Store",
            Guid.NewGuid(),
            Now
        );

    private static void AddItem(
        WatchedStore store,
        IReadOnlyList<string> userInputDefinitions,
        bool blockedOnInput = false
    )
    {
        var item = new StoreItem(Guid.NewGuid(), store.Id, CreateApiData(userInputDefinitions));
        if (blockedOnInput)
        {
            var entry = new WishlistEntry(Guid.NewGuid(), store.UserId, item.Id, Now);
            entry.EnableAutoRedeem(Now);
            entry.BlockOnMissingInput(Now);
            // The nav property is populated by the repository's ThenInclude in production.
            typeof(StoreItem).GetProperty(nameof(StoreItem.WishlistEntry))!.SetValue(item, entry);
        }
        store.AddItem(item);
    }

    private static StoreItemApiData CreateApiData(IReadOnlyList<string> userInputDefs) =>
        new(
            StoreProvider.StreamElements,
            new ProviderItemId($"se-{Guid.NewGuid()}"),
            new StoreExternalId("ch-1"),
            "Item",
            null,
            "perk",
            100,
            "default",
            true,
            false,
            true,
            false,
            false,
            userInputDefs,
            [],
            new ItemQuantity(-1, -1),
            new ItemCooldown(0, 0, 0),
            Now,
            null,
            Now
        );
}
