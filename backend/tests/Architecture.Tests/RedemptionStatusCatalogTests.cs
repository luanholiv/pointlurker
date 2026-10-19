using PointLurker.Core.Domain.Stores;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// A redemption's status reaches the screen as a catalog lookup keyed by the enum member's own name:
/// the API sends <c>Status</c> as <c>Enum.ToString()</c> and the history page interpolates it into
/// <c>redemption.status.&lt;member&gt;</c>. Nothing in either stack spells those keys out, so nothing
/// failed when the two sides disagreed.
/// <para>
/// Every other guard is blind here by construction. The frontend's <c>catalogCompleteness.test.ts</c>
/// compares the two locales to each other, so a renamed member leaves both wrong in the same way and
/// passes. <c>deadKeys.test.ts</c> allowlists the whole <c>redemption.status.</c> prefix precisely
/// because the keys are reached by a runtime value. <c>ErrorCodeCatalogKeyTests</c> looks only under
/// the catalog's <c>errors</c> object, and a status is not an error code. This is the guard those
/// three delegate to.
/// </para>
/// <para>
/// It is deliberately only half of the loop: it holds the enum to the catalog, and the frontend's own
/// <c>redemptionStatuses.test.ts</c> holds the catalog to the TypeScript list. Splitting it that way
/// is not stylistic — CI path-filters the backend job to <c>backend/**</c>, so a guard living only
/// here would never run on a pull request that changed only the frontend copy, which is the direction
/// that drifts.
/// </para>
/// </summary>
public class RedemptionStatusCatalogTests
{
    private const string CatalogRoot = LocaleCatalogFile.CatalogRoot;

    /// <summary>
    /// The status labels sit at the catalog root, not under <c>errors</c>. The catalog also holds an
    /// unrelated <c>errors.redemption.failure.*</c> node; this is not it.
    /// </summary>
    private const string StatusSection = "redemption.status";

    private static readonly string[] DeclaredStatuses = Enum.GetNames<RedemptionStatus>();

    [Theory]
    [InlineData("en-US.json")]
    [InlineData("pt-BR.json")]
    public void Every_redemption_status_has_a_catalog_key(string catalogFile)
    {
        var keys = LocaleCatalogFile.Keys(catalogFile, StatusSection);

        var missing = DeclaredStatuses
            .Where(status => !keys.Contains(status))
            .OrderBy(status => status, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"""
            {nameof(Every_redemption_status_has_a_catalog_key)}: {missing.Length} RedemptionStatus
            member(s) with no `{StatusSection}.<member>` key in {CatalogRoot}/{catalogFile}.
            The page interpolates the member name into the key, so a member the catalog cannot
            resolve renders as the raw identifier — and because the lookup passes a `defaultValue`,
            it does so silently, with no missing-key warning even in development.
            Missing:
            {string.Join("\n", missing)}
            """
        );
    }

    [Theory]
    [InlineData("en-US.json")]
    [InlineData("pt-BR.json")]
    public void Every_redemption_status_catalog_key_matches_a_declared_member(string catalogFile)
    {
        var keys = LocaleCatalogFile.Keys(catalogFile, StatusSection);

        var stale = keys.Where(key => !DeclaredStatuses.Contains(key, StringComparer.Ordinal))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"""
            {nameof(Every_redemption_status_catalog_key_matches_a_declared_member)}: {stale.Length}
            key(s) under `{StatusSection}` in {CatalogRoot}/{catalogFile} with no matching
            RedemptionStatus member. A label for a status that no longer exists is unreachable, and
            the frontend's dead-key test allowlists this whole prefix, so nothing else would say so.
            Delete the key, or declare the member it is waiting for.
            Unmatched:
            {string.Join("\n", stale)}
            """
        );
    }

    /// <summary>
    /// Guards the guards: reflection returning nothing would make both checks above pass while
    /// comparing an empty set against the catalog. The catalog side has the same protection inside
    /// <see cref="LocaleCatalogFile"/>, which throws rather than returning an empty section.
    /// </summary>
    [Fact]
    public void The_enum_scan_is_not_empty()
    {
        Assert.True(
            DeclaredStatuses.Length > 0,
            $"""
            {nameof(The_enum_scan_is_not_empty)}: RedemptionStatus reported no members, which cannot
            be right. Reflection has stopped seeing the enum, so the guards in this class are
            comparing an empty set against the catalog and passing without checking anything.
            """
        );
    }
}
