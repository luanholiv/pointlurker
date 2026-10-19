using System.Text.RegularExpressions;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// A comment states the mechanism; it does not name the tracker item the mechanism came from. The
/// number goes stale as soon as the tracker moves, and it belongs in the commit message or the pull
/// request body — the history, which <c>git blame</c> already reaches.
/// <para>
/// Comments are not AST nodes and no analyser selector can reach them, so this reads the prose
/// directly through <see cref="SourceScan.CSharpComments"/> — and only the prose. Every pattern and
/// fixture here lives in a string literal, which that scan blanks, so stating the rule does not
/// break it. It is the same property that keeps the Discord-tag test data out of the guard: a
/// literal is not a comment.
/// </para>
/// <para>
/// The frontend half of the same rule is the local <c>no-tracker-refs</c> ESLint rule. Two guards
/// rather than one because CI filters its jobs by path, so neither stack's gate runs on the other
/// stack's changes.
/// </para>
/// <para>
/// Known and deliberate gaps: a tracker word followed by a single undecorated digit is not matched,
/// because that shape cannot be told from prose counting lines; and a wrapped citation is reported
/// at the line carrying the number rather than the line the sentence starts on.
/// </para>
/// </summary>
public class CommentConventionTests
{
    /// <summary>
    /// Scanning <c>backend/</c> reaches src and tests together. The floor sits well under the real
    /// total; it is here only to fail loudly if the scan is ever pointed somewhere empty.
    /// </summary>
    private const int MinimumScannedFiles = 400;

    private const string EditorConfigFile = ".editorconfig";

    private const string Remedy =
        "say what broke instead — a tracker reference belongs in the commit message or pull request body";

    /// <summary>
    /// A tracker word joined to a number, then a number that can only be a tracker reference.
    /// <para>
    /// The first pattern takes the number two ways. With an explicit marker between them any digits
    /// count. Without one the number has to be dotted or at least two digits, which is what keeps
    /// prose out of it: a sentence saying a task runs three lines below is not a citation, while a
    /// dotted plan number is. A period is deliberately not a marker, so a method group on the task
    /// type cannot be read as one.
    /// </para>
    /// <para>
    /// The second pattern needs at least two digits directly after the marker, and a word character
    /// on neither side. That is what excludes a colour, a Discord tag, and a link fragment — none of
    /// which reach a comment scan anyway, but the pattern should be honest on its own.
    /// </para>
    /// </summary>
    private static readonly (Regex Pattern, string Explanation)[] TrackerReferences =
    [
        (
            new Regex(
                @"\b(?:issues?|tasks?|tickets?|bugs?|stor(?:y|ies)|epics?|prs?|gh)\b"
                    + @"(?:[ \t]*[#:-][ \t]*[0-9]|[ \t]+(?:[0-9]+(?:\.[0-9]+)+[a-z]?|[0-9]{2,}))",
                RegexOptions.IgnoreCase | RegexOptions.Compiled
            ),
            $"comment names a tracker item — {Remedy}"
        ),
        (
            new Regex(
                @"(^|[^0-9A-Za-z_])#[0-9]{2,}(?![0-9A-Za-z_-])",
                RegexOptions.Multiline | RegexOptions.Compiled
            ),
            $"comment carries a bare tracker number — {Remedy}"
        ),
    ];

    [Fact]
    public void Comments_do_not_name_tracker_items()
    {
        var violations = Distinct(
            SourceScan.CSharpComments(SourcePaths.BackendRoot).SelectMany(Violations)
        );

        Assert.True(
            violations.Length == 0,
            $"""
            {nameof(
                Comments_do_not_name_tracker_items
            )}: Found {violations.Length} tracker reference(s) in comments.
            There is no exemption list and no inline opt-out — reword the comment.
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    /// <summary>
    /// The style config is prose at the top and two settings at the bottom, and the prose drifted
    /// the same way the code comments did. Only its <c>#</c> lines are read, so a section header or
    /// a setting value cannot trip the guard.
    /// </summary>
    [Fact]
    public void Style_configuration_comments_do_not_name_tracker_items()
    {
        var violations = Distinct(
            HashCommentViolations(
                File.ReadAllText(SourcePaths.BackendFile(EditorConfigFile)),
                EditorConfigFile
            )
        );

        Assert.True(
            violations.Length == 0,
            $"""
            {nameof(
                Style_configuration_comments_do_not_name_tracker_items
            )}: Found {violations.Length} tracker reference(s) in comments.
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    /// <summary>
    /// Fails if the comment scan finds nothing to read, so a moved folder cannot switch the guards
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
    /// The shapes this repository already writes, none of which is a citation. Each is a string
    /// literal, so the guard scanning this very file sees no comment here to flag.
    /// </summary>
    [Theory]
    [InlineData("// Slate is #1e293b and white is #fff.")]
    [InlineData("// Anchored at https://example.com/spec#L120.")]
    [InlineData("// Every query it issues, so an extra call restates the default.")]
    [InlineData("// RFC 7807, react-router 7.18.2, IDE0290 and EventId 4001 are not citations.")]
    [InlineData("// The same task 3 lines below repeats it.")]
    [InlineData("// A Task.Delay(2000) in a test is timeout jiggling.")]
    [InlineData("""var tag = "luan#1234"; // a Discord tag is data, not a citation""")]
    [InlineData("#region Named\n#pragma warning disable\n#endregion")]
    public void Shapes_this_repository_already_uses_are_not_tracker_references(string source) =>
        Assert.Empty(Violations(Scan(source)));

    /// <summary>
    /// Every citation shape that had accumulated in this repository, so a later widening of the
    /// patterns cannot quietly stop catching one of them.
    /// </summary>
    [Theory]
    [InlineData("// not object — see issue #110.")]
    [InlineData("/// Dependency injection uses primary constructors (issue #105).")]
    [InlineData("// a broad allowlist here recreates the #143 blind spot.")]
    [InlineData("/* Task 6.6 — Catalog completeness test. */")]
    [InlineData("// the same regression task 6.9a removed from the redemption history.")]
    [InlineData("/// pull them from (issue\n/// #96). A constructor parameter")]
    [InlineData("// tracked as gh-1234")]
    public void A_tracker_reference_in_a_comment_is_reported(string source) =>
        Assert.NotEmpty(Violations(Scan(source)));

    [Theory]
    [InlineData("# Single-purpose config: the constructor-style rule from issue #105.", 1)]
    [InlineData("# Scoped to src/ only: fixtures legitimately assign injected fields.", 0)]
    [InlineData("[src/**/*.cs]\ndotnet_diagnostic.IDE0290.severity = warning", 0)]
    public void Hash_commented_configuration_is_read_the_same_way(string source, int expected) =>
        Assert.Equal(expected, Distinct(HashCommentViolations(source, EditorConfigFile)).Length);

    private static ScannedFile Scan(string source) =>
        new("Fixture.cs", SourceScan.CommentCanvas(source));

    private static IEnumerable<(string Key, string Text)> Violations(ScannedFile file)
    {
        foreach (var (pattern, explanation) in TrackerReferences)
        {
            foreach (Match match in pattern.Matches(file.Text))
            {
                var line = file.LineAt(match.Index);

                yield return (
                    $"{file.RelativePath}:{line}",
                    Describe(file.RelativePath, line, explanation, file.ExcerptAt(match.Index))
                );
            }
        }
    }

    /// <summary>
    /// The same patterns over an INI-style config, where a comment is a whole line opened by
    /// <c>#</c>. There are no inline comments to separate out, so the line is the unit.
    /// </summary>
    private static IEnumerable<(string Key, string Text)> HashCommentViolations(
        string text,
        string relativePath
    )
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];

            if (!line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            foreach (var (pattern, explanation) in TrackerReferences)
            {
                if (pattern.IsMatch(line))
                {
                    yield return (
                        $"{relativePath}:{index + 1}",
                        Describe(relativePath, index + 1, explanation, line.Trim())
                    );
                }
            }
        }
    }

    /// <summary>
    /// A named tracker item almost always carries a bare number too, so one comment trips both
    /// patterns. Report the line once, under whichever pattern reached it first.
    /// </summary>
    private static string[] Distinct(IEnumerable<(string Key, string Text)> violations) =>
        [.. violations.DistinctBy(violation => violation.Key).Select(violation => violation.Text)];

    private static string Describe(
        string relativePath,
        int line,
        string explanation,
        string excerpt
    ) => $"{relativePath}:{line}: {explanation}{Environment.NewLine}    {excerpt}";
}
