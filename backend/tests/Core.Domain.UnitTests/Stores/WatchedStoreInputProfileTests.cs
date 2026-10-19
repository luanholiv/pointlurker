using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class WatchedStoreInputProfileTests
{
    [Fact]
    public void New_store_has_empty_input_collections()
    {
        var store = CreateStore();

        store.InputValues.Should().BeEmpty();
        store.InputAliases.Should().BeEmpty();
    }

    [Fact]
    public void UpsertValue_adds_new_entry()
    {
        var store = CreateStore();
        var ts = DateTimeOffset.UtcNow;

        store.UpsertValue("Twitch Username", "cipher-1", ts);

        store.InputValues.Should().ContainSingle();
        var v = store.InputValues[0];
        v.LabelCanonical.Should().Be("Twitch Username");
        v.EncryptedValue.Should().Be("cipher-1");
        v.UpdatedAt.Should().Be(ts);
    }

    [Fact]
    public void UpsertValue_replaces_existing_by_canonical_case_insensitive()
    {
        var store = CreateStore();
        var t1 = DateTimeOffset.UtcNow;
        var t2 = t1.AddMinutes(1);

        store.UpsertValue("Twitch Username", "old", t1);
        store.UpsertValue("twitch username", "new", t2);

        store.InputValues.Should().ContainSingle();
        store.InputValues[0].EncryptedValue.Should().Be("new");
        store.InputValues[0].UpdatedAt.Should().Be(t2);
    }

    [Fact]
    public void UpsertValue_normalizes_canonical_label_whitespace()
    {
        var store = CreateStore();

        store.UpsertValue("  Twitch    Username  ", "cipher", DateTimeOffset.UtcNow);

        store.InputValues[0].LabelCanonical.Should().Be("Twitch Username");
    }

    [Fact]
    public void UpsertValue_throws_on_empty_label()
    {
        var store = CreateStore();

        var act = () => store.UpsertValue("   ", "cipher", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpsertValue_throws_on_empty_ciphertext()
    {
        var store = CreateStore();

        var act = () => store.UpsertValue("Label", "", DateTimeOffset.UtcNow);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveValue_returns_true_when_removed_and_idempotent()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);

        store.RemoveValue("twitch username").Should().BeTrue();
        store.InputValues.Should().BeEmpty();
        store.RemoveValue("twitch username").Should().BeFalse();
    }

    [Fact]
    public void HasValue_and_GetCiphertext_match_canonical()
    {
        var store = CreateStore();
        store.UpsertValue("Steam ID", "cipher-x", DateTimeOffset.UtcNow);

        store.HasValue("steam id").Should().BeTrue();
        store.GetCiphertext("STEAM ID").Should().Be("cipher-x");
        store.HasValue("Discord Tag").Should().BeFalse();
        store.GetCiphertext("Discord Tag").Should().BeNull();
    }

    [Fact]
    public void UpsertAlias_adds_and_replaces_by_raw_label()
    {
        var store = CreateStore();

        store.UpsertAlias("Twitchh Usrname", "Twitch Username");
        store.UpsertAlias("twitchh usrname", "Twitch User");

        store.InputAliases.Should().ContainSingle();
        store.InputAliases[0].LabelCanonical.Should().Be("Twitch User");
    }

    [Fact]
    public void UpsertAlias_normalizes_both_labels()
    {
        var store = CreateStore();

        store.UpsertAlias("  raw   label ", "  Canonical  Label ");

        store.InputAliases[0].RawLabel.Should().Be("raw label");
        store.InputAliases[0].LabelCanonical.Should().Be("Canonical Label");
    }

    [Fact]
    public void UpsertAlias_throws_on_empty_inputs()
    {
        var store = CreateStore();

        var actRaw = () => store.UpsertAlias(" ", "canonical");
        var actCanonical = () => store.UpsertAlias("raw", " ");

        actRaw.Should().Throw<ArgumentException>();
        actCanonical.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RemoveAlias_idempotent()
    {
        var store = CreateStore();
        store.UpsertAlias("raw", "canonical");

        store.RemoveAlias("RAW").Should().BeTrue();
        store.InputAliases.Should().BeEmpty();
        store.RemoveAlias("raw").Should().BeFalse();
    }

    private static WatchedStore CreateStore() =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("test-channel"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
}
