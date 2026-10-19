using System.Text.RegularExpressions;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// An error code is a contract with the frontend and with whoever reads a response, so
/// <c>docs/ERROR-CODES.md</c> is part of the code, not commentary about it. These tests keep the
/// reference and <c>ErrorCodes</c> in step in both directions: a code added without documentation
/// fails, and documentation left behind by a removed code fails too.
/// </summary>
public class ErrorCodeDocumentationTests
{
    private const string ReferencePath = "docs/ERROR-CODES.md";

    /// <summary>
    /// Codes are documented as inline code spans, which is also how the reference reads today.
    /// A span anywhere in the reference counts — table row or prose alike — because a retired code
    /// earns a sentence explaining that it is retired rather than a row promising an endpoint still
    /// emits it, and <c>auth.unauthorized</c> and its four neighbours are documented exactly that
    /// way. Demanding a table row would fail those five, and inventing rows for codes nothing emits
    /// would make the reference lie to keep a test quiet.
    /// </summary>
    private static readonly Regex DocumentedCode = new(
        @"`(?<code>[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+)`",
        RegexOptions.Compiled
    );

    [Fact]
    public void Every_error_code_is_documented_in_the_reference()
    {
        var reference = File.ReadAllText(SourcePaths.RepositoryFile(ReferencePath));

        var undocumented = ErrorCodeCatalog
            .Codes.Where(code => !reference.Contains($"`{code}`", StringComparison.Ordinal))
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            undocumented.Length == 0,
            $"""
            {nameof(Every_error_code_is_documented_in_the_reference)}: {undocumented.Length} code(s)
            declared in ErrorCodes but absent from {ReferencePath}.
            Document each one as `<code>` under its namespace section — a code the frontend cannot
            look up reaches the user as a raw identifier.
            Undocumented:
            {string.Join("\n", undocumented)}
            """
        );
    }

    [Fact]
    public void Every_code_documented_in_the_reference_is_declared_in_ErrorCodes()
    {
        var reference = File.ReadAllText(SourcePaths.RepositoryFile(ReferencePath));

        var stale = DocumentedCode
            .Matches(reference)
            .Select(match => match.Groups["code"].Value)
            .Distinct(StringComparer.Ordinal)
            // `errors.*` spans are frontend catalog keys, not codes, and a namespace such as
            // `redemption.failure` is written out when a section is introduced.
            .Where(code =>
                !code.StartsWith("errors.", StringComparison.Ordinal)
                && !ErrorCodeCatalog.Codes.Contains(code)
                && !ErrorCodeCatalog.Codes.Any(declared =>
                    declared.StartsWith($"{code}.", StringComparison.Ordinal)
                )
            )
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            stale.Length == 0,
            $"""
            {nameof(
                Every_code_documented_in_the_reference_is_declared_in_ErrorCodes
            )}: {stale.Length}
            documented code(s) no longer exist in ErrorCodes.
            Either the code was renamed or removed and {ReferencePath} still advertises it, or the
            constant is missing. Fix whichever is wrong — a documented code the backend never emits
            is a promise nothing keeps.
            Stale:
            {string.Join("\n", stale)}
            """
        );
    }
}
