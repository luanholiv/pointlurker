using FluentAssertions;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Application.UnitTests.Stores;

public sealed class FuzzyInputLabelResolverTests
{
    [Fact]
    public void Returns_alias_match_with_confidence_100()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);
        store.UpsertAlias("twitchh usrname", "Twitch Username");

        var sut = CreateResolver(threshold: 85);

        var result = sut.Resolve("twitchh usrname", store);

        result.Should().BeOfType<ResolutionResult.Resolved>();
        var resolved = (ResolutionResult.Resolved)result;
        resolved.Canonical.Should().Be("Twitch Username");
        resolved.Confidence.Should().Be(100);
        resolved.Source.Should().Be(ResolutionSource.Alias);
    }

    [Fact]
    public void Alias_takes_precedence_over_exact()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);
        store.UpsertValue("Twitter Handle", "cipher2", DateTimeOffset.UtcNow);
        store.UpsertAlias("Twitch Username", "Twitter Handle");

        var sut = CreateResolver(threshold: 85);

        var result = sut.Resolve("Twitch Username", store);

        result.Should().BeOfType<ResolutionResult.Resolved>();
        var resolved = (ResolutionResult.Resolved)result;
        resolved.Canonical.Should().Be("Twitter Handle");
        resolved.Source.Should().Be(ResolutionSource.Alias);
    }

    [Fact]
    public void Exact_match_ignores_case_and_collapses_whitespace()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);

        var sut = CreateResolver(threshold: 85);

        var result = sut.Resolve("  twitch   username ", store);

        result.Should().BeOfType<ResolutionResult.Resolved>();
        var resolved = (ResolutionResult.Resolved)result;
        resolved.Canonical.Should().Be("Twitch Username");
        resolved.Confidence.Should().Be(100);
        resolved.Source.Should().Be(ResolutionSource.Exact);
    }

    [Fact]
    public void Exact_match_wins_over_equal_scoring_fuzzy_candidate()
    {
        var store = CreateStore();
        // TokenSetRatio returns 100 for both candidates — identical token sets. Insertion
        // order decides under a strict `>` comparison, so the exact match must be tried
        // before the fuzzy loop or the wrong slot wins.
        store.UpsertValue("Username Twitch", "cipher", DateTimeOffset.UtcNow);
        store.UpsertValue("Twitch Username", "cipher2", DateTimeOffset.UtcNow);

        var sut = CreateResolver(threshold: 85);

        var result = sut.Resolve("Twitch Username", store);

        result.Should().BeOfType<ResolutionResult.Resolved>();
        var resolved = (ResolutionResult.Resolved)result;
        resolved.Canonical.Should().Be("Twitch Username");
        resolved.Source.Should().Be(ResolutionSource.Exact);
    }

    [Fact]
    public void Fuzzy_match_above_threshold_resolves()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);

        var sut = CreateResolver(threshold: 85);

        // A genuine near-match: misspelled on both tokens, so it can never be Exact.
        var result = sut.Resolve("twitchh usrname", store);

        result.Should().BeOfType<ResolutionResult.Resolved>();
        var resolved = (ResolutionResult.Resolved)result;
        resolved.Canonical.Should().Be("Twitch Username");
        resolved.Confidence.Should().BeGreaterThanOrEqualTo(85);
        resolved.Confidence.Should().BeLessThan(100);
        resolved.Source.Should().Be(ResolutionSource.Fuzzy);
    }

    [Fact]
    public void Fuzzy_match_below_threshold_returns_unresolved()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);

        var sut = CreateResolver(threshold: 99);

        var result = sut.Resolve("xyz unrelated abc", store);

        result.Should().BeOfType<ResolutionResult.Unresolved>();
    }

    [Fact]
    public void Threshold_is_configurable()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);

        var strict = CreateResolver(threshold: 100);
        var lax = CreateResolver(threshold: 50);

        strict.Resolve("Twitch Usrnme", store).Should().BeOfType<ResolutionResult.Unresolved>();
        lax.Resolve("Twitch Usrnme", store).Should().BeOfType<ResolutionResult.Resolved>();
    }

    [Fact]
    public void Normalizes_whitespace_for_alias_lookup()
    {
        var store = CreateStore();
        store.UpsertAlias("raw label", "Canonical Label");
        store.UpsertValue("Canonical Label", "cipher", DateTimeOffset.UtcNow);

        var sut = CreateResolver(threshold: 85);

        var result = sut.Resolve("  raw    label  ", store);

        result.Should().BeOfType<ResolutionResult.Resolved>();
        ((ResolutionResult.Resolved)result).Source.Should().Be(ResolutionSource.Alias);
    }

    [Fact]
    public void Empty_label_returns_unresolved()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);

        var sut = CreateResolver(threshold: 85);

        var result = sut.Resolve("   ", store);

        result.Should().BeOfType<ResolutionResult.Unresolved>();
        var unresolved = (ResolutionResult.Unresolved)result;
        unresolved.BestScore.Should().Be(0);
        unresolved.BestCanonical.Should().BeNull();
    }

    [Fact]
    public void No_candidates_returns_unresolved_with_null_best_canonical()
    {
        var store = CreateStore();
        var sut = CreateResolver(threshold: 85);

        var result = sut.Resolve("Twitch Username", store);

        result.Should().BeOfType<ResolutionResult.Unresolved>();
        var unresolved = (ResolutionResult.Unresolved)result;
        unresolved.BestScore.Should().Be(0);
        unresolved.BestCanonical.Should().BeNull();
    }

    [Fact]
    public void Below_threshold_unresolved_carries_best_candidate_canonical()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);

        // Threshold 100 means anything short of a perfect token-set match stays
        // unresolved, yet the best (sub-threshold) candidate must still be surfaced.
        var sut = CreateResolver(threshold: 100);

        var result = sut.Resolve("Twitch Usernam", store);

        result.Should().BeOfType<ResolutionResult.Unresolved>();
        var unresolved = (ResolutionResult.Unresolved)result;
        unresolved.BestCanonical.Should().Be("Twitch Username");
        unresolved.BestScore.Should().BeInRange(1, 99);
    }

    [Fact]
    public void Best_canonical_is_the_highest_scoring_candidate()
    {
        var store = CreateStore();
        store.UpsertValue("Twitch Username", "cipher", DateTimeOffset.UtcNow);
        store.UpsertValue("Steam ID", "cipher2", DateTimeOffset.UtcNow);

        var sut = CreateResolver(threshold: 100);

        var result = sut.Resolve("Steem ID", store);

        result.Should().BeOfType<ResolutionResult.Unresolved>();
        ((ResolutionResult.Unresolved)result).BestCanonical.Should().Be("Steam ID");
    }

    private static FuzzyInputLabelResolver CreateResolver(int threshold) =>
        new(Options.Create(new InputResolutionOptions { MatchThreshold = threshold }));

    private static WatchedStore CreateStore() =>
        new(
            Guid.NewGuid(),
            StoreProvider.StreamElements,
            new StoreExternalId("ch-1"),
            "Test Store",
            Guid.NewGuid(),
            DateTimeOffset.UtcNow
        );
}
