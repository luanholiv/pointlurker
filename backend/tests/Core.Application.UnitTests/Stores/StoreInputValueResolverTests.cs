using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class StoreInputValueResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    private readonly FuzzyInputLabelResolver _labelResolver = new(
        Options.Create(new InputResolutionOptions { MatchThreshold = 85 })
    );
    private readonly ISecretProtector _protector = Substitute.For<ISecretProtector>();
    private readonly StoreInputValueResolver _sut;

    public StoreInputValueResolverTests()
    {
        _sut = new StoreInputValueResolver(_labelResolver, _protector);
    }

    [Fact]
    public void Resolves_every_definition_in_order()
    {
        var store = CreateStore();
        store.UpsertValue("Discord Tag", "ct-discord", Now);
        store.UpsertValue("Nick (IGN)", "ct-nick", Now);
        _protector.Unprotect("ct-discord").Returns("luan#1234");
        _protector.Unprotect("ct-nick").Returns("luanzin");
        var item = CreateItem(store, "Discord Tag", "Nick (IGN)");

        var result = _sut.Resolve(item, store);

        result
            .Should()
            .BeOfType<StoreInputResolution.Resolved>()
            .Which.Values.Should()
            .Equal(
                new ProviderInputValue("Discord Tag", "luan#1234"),
                new ProviderInputValue("Nick (IGN)", "luanzin")
            );
    }

    [Fact]
    public void Reports_a_label_the_store_profile_cannot_resolve_as_missing()
    {
        var store = CreateStore();
        store.UpsertValue("Discord Tag", "ct-discord", Now);
        _protector.Unprotect("ct-discord").Returns("luan#1234");
        var item = CreateItem(store, "Discord Tag", "Postal Address");

        var result = _sut.Resolve(item, store);

        result
            .Should()
            .BeOfType<StoreInputResolution.Missing>()
            .Which.Labels.Should()
            .Equal("Postal Address");
    }

    /// <summary>
    /// An alias maps a raw label onto a canonical one that may have no stored value yet — the label
    /// resolves but there is nothing to decrypt, which is still a coverage miss.
    /// </summary>
    [Fact]
    public void Reports_a_resolved_label_without_a_stored_value_as_missing()
    {
        var store = CreateStore();
        store.UpsertAlias("Discord Tag", "discord");
        var item = CreateItem(store, "Discord Tag");

        var result = _sut.Resolve(item, store);

        result
            .Should()
            .BeOfType<StoreInputResolution.Missing>()
            .Which.Labels.Should()
            .Equal("Discord Tag");
    }

    [Fact]
    public void Accumulates_every_missing_label_in_definition_order()
    {
        var store = CreateStore();
        var item = CreateItem(store, "Postal Address", "Phone Number");

        var result = _sut.Resolve(item, store);

        result
            .Should()
            .BeOfType<StoreInputResolution.Missing>()
            .Which.Labels.Should()
            .Equal("Postal Address", "Phone Number");
    }

    [Fact]
    public void Reports_a_failing_decryption_with_its_label_and_exception()
    {
        var boom = new InvalidOperationException("key ring rotated");
        var store = CreateStore();
        store.UpsertValue("Discord Tag", "ct-discord", Now);
        _protector.Unprotect("ct-discord").Throws(boom);
        var item = CreateItem(store, "Discord Tag");

        var result = _sut.Resolve(item, store);

        var failure = result.Should().BeOfType<StoreInputResolution.DecryptionFailed>().Subject;
        failure.Label.Should().Be("Discord Tag");
        failure.Exception.Should().BeSameAs(boom);
    }

    /// <summary>
    /// Decryption failure short-circuits: it is a hard fault on a value the store *does* hold, and
    /// it outranks a coverage miss recorded earlier in the same pass.
    /// </summary>
    [Fact]
    public void Decryption_failure_wins_over_an_earlier_missing_label()
    {
        var store = CreateStore();
        store.UpsertValue("Discord Tag", "ct-discord", Now);
        _protector.Unprotect("ct-discord").Throws(new InvalidOperationException("nope"));
        var item = CreateItem(store, "Postal Address", "Discord Tag");

        var result = _sut.Resolve(item, store);

        result
            .Should()
            .BeOfType<StoreInputResolution.DecryptionFailed>()
            .Which.Label.Should()
            .Be("Discord Tag");
    }

    [Fact]
    public void An_item_with_no_input_definitions_resolves_to_no_values()
    {
        var store = CreateStore();
        var item = CreateItem(store);

        var result = _sut.Resolve(item, store);

        result.Should().BeOfType<StoreInputResolution.Resolved>().Which.Values.Should().BeEmpty();
    }

    private static WatchedStore CreateStore() =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("channel-1"),
            "Store",
            Guid.NewGuid(),
            Now
        );

    private static StoreItem CreateItem(WatchedStore store, params string[] inputDefinitions)
    {
        var item = new StoreItem(
            Guid.NewGuid(),
            store.Id,
            new StoreItemApiData(
                StoreProvider.StreamElements,
                new ProviderItemId("se-item-1"),
                store.ExternalId,
                "Test Item",
                null,
                "perk",
                100,
                "default",
                true,
                false,
                true,
                false,
                false,
                inputDefinitions,
                [],
                new ItemQuantity(-1, -1),
                new ItemCooldown(0, 0, 0),
                Now,
                null,
                Now
            )
        );
        item.ClearDomainEvents();
        return item;
    }
}
