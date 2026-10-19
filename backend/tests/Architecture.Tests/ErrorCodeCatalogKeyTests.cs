namespace PointLurker.Architecture.Tests;

/// <summary>
/// A code is only half of the contract: the other half is the sentence the frontend renders for it.
/// When the key is missing, the catalog lookup misses and the identifier itself reaches the user —
/// literally <c>wishlist.already_exists</c> on screen where a sentence belongs.
/// <para>
/// The frontend's own <c>catalogCompleteness.test.ts</c> compares the two locales to each other and
/// <c>deadKeys.test.ts</c> looks for keys no component names, but neither can see the backend, so
/// nothing failed when a code shipped without a key. This closes that direction.
/// </para>
/// <para>
/// Note what it deliberately does not claim: that a code is ever actually emitted. Keys can be
/// correct in both locales and still be unreachable because nothing calls the code that produces
/// them — the state the Identity mapping sat in. Whether a mapping is wired is a question about
/// call graphs, not about names, and it belongs to the integration tests that drive the endpoints.
/// </para>
/// </summary>
public class ErrorCodeCatalogKeyTests
{
    private const string CatalogRoot = LocaleCatalogFile.CatalogRoot;
    private const string ErrorsSection = "errors";

    [Theory]
    [InlineData("en-US.json")]
    [InlineData("pt-BR.json")]
    public void Every_error_code_has_a_catalog_key(string catalogFile)
    {
        var keys = LocaleCatalogFile.Keys(catalogFile, ErrorsSection);

        var missing = ErrorCodeCatalog
            .Codes.Where(code => !keys.Contains(code))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"""
            {nameof(Every_error_code_has_a_catalog_key)}: {missing.Length} code(s) declared in
            ErrorCodes with no `errors.<code>` key in {CatalogRoot}/{catalogFile}.
            A code the catalog cannot resolve is rendered as the raw identifier.
            Missing:
            {string.Join("\n", missing)}
            """
        );
    }

    [Theory]
    [InlineData("en-US.json")]
    [InlineData("pt-BR.json")]
    public void Every_catalog_key_matches_a_declared_error_code(string catalogFile)
    {
        var keys = LocaleCatalogFile.Keys(catalogFile, ErrorsSection);

        var stale = keys.Where(key => !ErrorCodeCatalog.Codes.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"""
            {nameof(Every_catalog_key_matches_a_declared_error_code)}: {stale.Length} key(s) under
            `errors.` in {CatalogRoot}/{catalogFile} with no matching ErrorCodes constant.
            The frontend's dead-key test allowlists the whole `errors.` prefix because those keys are
            reached by runtime code rather than named in source, so this is the only guard that sees
            them. Delete the key, or declare the code it is waiting for.
            Unmatched:
            {string.Join("\n", stale)}
            """
        );
    }
}
