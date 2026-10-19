using System.Text.RegularExpressions;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// A comment states the rule the code holds; it does not recount the shape the code held before.
/// That account rots the way a tracker number does — nothing keeps it true once the code moves
/// again, and it does not survive a copy of the code — while <c>git blame</c> already reaches the
/// commit message, which is where it belongs.
/// <para>
/// The same scan and the same shape as <see cref="CommentConventionTests"/>: prose only, read
/// through <see cref="SourceScan.CSharpComments"/>, with every pattern and fixture in a string
/// literal that the scan blanks, so stating the rule cannot break it.
/// </para>
/// <para>
/// Deliberately narrow. Only wording that can mean nothing else is matched. <c>no longer</c>,
/// <c>legacy</c> and a bare <c>now</c> read just as naturally as present-tense prose about what the
/// code does — "a rule it does not own", "a query that is not the current one" — so matching them
/// would cost more in rewording than the rule is worth. Passive voice is excluded outright, because
/// "the expression is used to build both" says nothing about any earlier version, and the exclusion
/// reaches across a wrapped line since a comment breaks wherever it happens to run long.
/// </para>
/// <para>
/// The frontend half is the local <c>no-comment-history</c> ESLint rule, for the reason
/// <see cref="CommentConventionTests"/> gives: CI filters its jobs by path, so neither stack's gate
/// runs on the other stack's changes.
/// </para>
/// </summary>
public class CommentHistoryConventionTests
{
    /// <summary>
    /// The same floor <see cref="CommentConventionTests"/> uses, and for the same reason: a guard
    /// that scans nothing reports no violations and reads as a pass.
    /// </summary>
    private const int MinimumScannedFiles = 400;

    private const string Remedy =
        "state the rule the code holds — the shape it held before belongs in the commit message";

    /// <summary>
    /// Four shapes, each of which can only be an account of superseded code.
    /// <para>
    /// The first excludes the auxiliaries that turn the phrase passive. The run between the
    /// auxiliary and the phrase is bounded rather than a single space so that a comment wrapped
    /// between the two words is still recognised: what sits in the gap is whitespace and the comment
    /// markers the scan preserves.
    /// </para>
    /// <para>
    /// The last three name a defect rather than describing one. A test that says which failure it
    /// pins is useful; a test that says a failure once existed is dated the moment it is fixed.
    /// </para>
    /// </summary>
    private static readonly (Regex Pattern, string Explanation)[] SupersededCode =
    [
        (
            new Regex(
                @"(?<!\b(?:is|are|was|were|be|been|being|get|gets|got|not|also|only|then)"
                    + @"[\s/*]{1,16})\bused to\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
            $"comment recounts what the code did before — {Remedy}"
        ),
        (
            new Regex(@"\bpreviously\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            $"comment dates a statement against an earlier version — {Remedy}"
        ),
        (
            new Regex(
                @"\bthe (?:bug|defect|regression|problem) (?:this|that)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
            $"comment names the defect a change closed — {Remedy}"
        ),
        (
            new Regex(@"\bregression test for\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            $"comment labels a test by the failure it was written for — {Remedy}"
        ),
    ];

    [Fact]
    public void Comments_do_not_recount_superseded_code()
    {
        var violations = Distinct(
            SourceScan.CSharpComments(SourcePaths.BackendRoot).SelectMany(Violations)
        );

        Assert.True(
            violations.Length == 0,
            $"""
            {nameof(
                Comments_do_not_recount_superseded_code
            )}: Found {violations.Length} account(s) of superseded code in comments.
            There is no exemption list and no inline opt-out — reword the comment.
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    /// <summary>
    /// Fails if the comment scan finds nothing to read, so a moved folder cannot switch the guard
    /// above off while still reporting a pass.
    /// </summary>
    [Fact]
    public void The_comment_scan_actually_covers_the_backend_source()
    {
        var files = SourceScan.CSharpComments(SourcePaths.BackendRoot);
        var trees = files.Select(file => file.RelativePath.Split('/')[0]).Distinct().Order();

        Assert.True(
            files.Count > MinimumScannedFiles && trees.SequenceEqual(["src", "tests"]),
            $"Expected the comment scan to cover all of src/ and tests/; found {files.Count} "
                + $"file(s) across [{string.Join(", ", trees)}] under {SourcePaths.BackendRoot}. "
                + "A guard that scans nothing passes vacuously."
        );
    }

    /// <summary>
    /// Present-tense prose the repository writes, including the wordings this rule deliberately
    /// leaves alone. Each is a string literal, so the guard scanning this very file sees no comment
    /// here to flag.
    /// </summary>
    [Theory]
    [InlineData("// The expression is used to build both halves of the query.")]
    [InlineData("/// the same expression is\n/// used to build both halves.")]
    [InlineData("// The rows answer a query that is no longer the current one.")]
    [InlineData("// Legacy callers reach the same endpoint.")]
    [InlineData("// Sorting goes to the server, which orders the whole history before slicing.")]
    [InlineData("/// Ordering parity with the client comparator this suite replaces.")]
    [InlineData("// Asserting that the page hides a row would assert a rule it does not own.")]
    public void Present_tense_prose_is_not_an_account_of_superseded_code(string source) =>
        Assert.Empty(Violations(Scan(source)));

    /// <summary>
    /// The shapes this repository had accumulated, so a later narrowing of the patterns cannot
    /// quietly stop catching one of them.
    /// </summary>
    [Theory]
    [InlineData("// Sorting used to reorder the twenty rows already on screen.")]
    [InlineData("/// Previously each repository call was its own commit.")]
    [InlineData("/* The bug this closes: the button looked inert rather than failed. */")]
    [InlineData("// Regression test for the blank toast body.")]
    [InlineData("/// The timeout sweep\n/// used to sit behind the token checks.")]
    public void An_account_of_superseded_code_is_reported(string source) =>
        Assert.NotEmpty(Violations(Scan(source)));

    private static ScannedFile Scan(string source) =>
        new("Fixture.cs", SourceScan.CommentCanvas(source));

    private static IEnumerable<(string Key, string Text)> Violations(ScannedFile file)
    {
        foreach (var (pattern, explanation) in SupersededCode)
        {
            foreach (Match match in pattern.Matches(file.Text))
            {
                var line = file.LineAt(match.Index);

                yield return (
                    $"{file.RelativePath}:{line}",
                    $"{file.RelativePath}:{line}: {explanation}{Environment.NewLine}    "
                        + file.ExcerptAt(match.Index)
                );
            }
        }
    }

    /// <summary>
    /// One sentence can trip two patterns, so report the line once, under whichever reached it
    /// first.
    /// </summary>
    private static string[] Distinct(IEnumerable<(string Key, string Text)> violations) =>
        [.. violations.DistinctBy(violation => violation.Key).Select(violation => violation.Text)];
}
