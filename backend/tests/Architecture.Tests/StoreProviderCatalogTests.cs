using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// A provider reaches the screen as a catalog lookup keyed by its slug: the API sends
/// <c>provider</c> as <see cref="StoreProviderSlugs.ToSlug"/> and the badge interpolates it into
/// <c>providers.&lt;slug&gt;</c>. Nothing in either stack spells those keys out.
/// <para>
/// Built exactly like <see cref="RedemptionStatusCatalogTests"/>, and for the same reason only half
/// of the loop: this holds the enum to the catalog, and the frontend's <c>storeProviders.test.ts</c>
/// holds the catalog to the TypeScript list. CI path-filters the backend job to <c>backend/**</c>,
/// so a guard living only here would never run on a frontend-only pull request.
/// </para>
/// </summary>
public class StoreProviderCatalogTests
{
    private const string CatalogRoot = LocaleCatalogFile.CatalogRoot;

    private const string ProviderSection = "providers";

    private static readonly string[] DeclaredSlugs = Enum.GetValues<StoreProvider>()
        .Select(provider => provider.ToSlug())
        .ToArray();

    [Theory]
    [InlineData("en-US.json")]
    [InlineData("pt-BR.json")]
    public void Every_provider_has_a_catalog_key(string catalogFile)
    {
        var keys = LocaleCatalogFile.Keys(catalogFile, ProviderSection);

        var missing = DeclaredSlugs
            .Where(slug => !keys.Contains(slug))
            .OrderBy(slug => slug, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"""
            {nameof(Every_provider_has_a_catalog_key)}: {missing.Length} StoreProvider member(s)
            with no `{ProviderSection}.<slug>` key in {CatalogRoot}/{catalogFile}. The badge
            interpolates the slug into the key, so a provider the catalog cannot resolve renders as
            the raw slug.
            Missing:
            {string.Join("\n", missing)}
            """
        );
    }

    [Theory]
    [InlineData("en-US.json")]
    [InlineData("pt-BR.json")]
    public void Every_provider_catalog_key_matches_a_declared_member(string catalogFile)
    {
        var keys = LocaleCatalogFile.Keys(catalogFile, ProviderSection);

        var stale = keys.Where(key => !DeclaredSlugs.Contains(key, StringComparer.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"""
            {nameof(Every_provider_catalog_key_matches_a_declared_member)}: {stale.Length} key(s)
            under `{ProviderSection}` in {CatalogRoot}/{catalogFile} with no matching StoreProvider
            member. The frontend's dead-key test allowlists this prefix, so nothing else would say so.
            Unmatched:
            {string.Join("\n", stale)}
            """
        );
    }

    /// <summary>Guards the guards: an empty enum scan would make both checks pass vacuously.</summary>
    [Fact]
    public void The_enum_scan_is_not_empty()
    {
        Assert.True(
            DeclaredSlugs.Length > 0,
            $"""
            {nameof(The_enum_scan_is_not_empty)}: StoreProvider reported no members, so the guards
            in this class are comparing an empty set against the catalog.
            """
        );
    }
}
