using System.Text.RegularExpressions;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// One hand-written C# file, ready to be pattern-matched. Comment-only lines are blanked so a
/// guard cannot be tripped by an example inside a doc comment, and blanking (rather than removing)
/// keeps every reported line number equal to the line number in the editor.
/// </summary>
/// <param name="RelativePath">Path relative to the scanned root, always with <c>/</c> separators.</param>
/// <param name="Text">File text with comment-only lines blanked and newlines normalised to <c>\n</c>.</param>
internal sealed record ScannedFile(string RelativePath, string Text)
{
    /// <summary>1-based line number of <paramref name="index"/> into <see cref="Text"/>.</summary>
    public int LineAt(int index) => Text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>The whole trimmed line containing <paramref name="index"/>, for the failure report.</summary>
    public string ExcerptAt(int index)
    {
        var start = index == 0 ? 0 : Text.LastIndexOf('\n', index - 1) + 1;
        var end = Text.IndexOf('\n', index);
        return Text[start..(end < 0 ? Text.Length : end)].Trim();
    }
}

/// <summary>
/// Reads the C# source of a project for the guards that cannot be expressed against compiled
/// metadata — inline string literals, argument shapes, call spellings, comment prose.
/// </summary>
internal static class SourceScan
{
    /// <summary>
    /// Comments and string literals in one alternation, so whichever opens first wins: the
    /// <c>//</c> inside <c>"https://example.com"</c> is swallowed by the string alternative, whose
    /// quote comes earlier, and never reads as the start of a comment.
    /// </summary>
    private static readonly Regex CommentOrStringLiteral = new(
        """
        //.*?$|/\*.*?\*/|"(?:\\.|[^"\\])*"|@"(?:[^"]|"")*"
        """,
        RegexOptions.Singleline | RegexOptions.Multiline | RegexOptions.Compiled
    );

    /// <summary>
    /// Every hand-written <c>.cs</c> file below <paramref name="root"/>, ordered by path, with
    /// comment-only lines blanked. Build output (<c>obj/</c>, <c>bin/</c>) is excluded: generated
    /// files are not authored here and their literals are not violations. Throws when the scan
    /// would come up empty, because a scanner with nothing to scan reports no violations — a
    /// silent pass is the failure mode this helper exists to prevent.
    /// </summary>
    public static IReadOnlyList<ScannedFile> CSharpFiles(string root) =>
        Read(root, BlankCommentLines);

    /// <summary>
    /// The inverse of <see cref="CSharpFiles"/>: every hand-written <c>.cs</c> file below
    /// <paramref name="root"/> with everything that is <i>not</i> a comment blanked out, for the
    /// guards whose subject is the prose rather than the code. A comment is not an AST node and
    /// cannot be matched as one, and matching raw file text instead would flag the very code that
    /// documents the rule.
    /// </summary>
    public static IReadOnlyList<ScannedFile> CSharpComments(string root) =>
        Read(root, CommentCanvas);

    private static IReadOnlyList<ScannedFile> Read(string root, Func<string, string> prepare)
    {
        var files = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(IsHandWritten)
            .OrderBy(relativePath => relativePath, StringComparer.Ordinal)
            .Select(relativePath => new ScannedFile(
                relativePath,
                prepare(File.ReadAllText(Path.Combine(root, relativePath)))
            ))
            .ToArray();

        if (files.Length == 0)
        {
            throw new InvalidOperationException(
                $"No hand-written C# files found under '{root}'. A guard that scans nothing passes "
                    + "vacuously, so this is a failure, not an empty result."
            );
        }

        return files;
    }

    /// <summary>
    /// Build output is not authored here. <c>publish/</c> joins <c>obj/</c> and <c>bin/</c> because
    /// a scan rooted at <c>backend/</c> — rather than at one project — walks the publish directory
    /// that the local verification script drops there.
    /// </summary>
    private static bool IsHandWritten(string relativePath) =>
        !relativePath.Split('/').Any(segment => segment is "obj" or "bin" or "publish");

    /// <summary>
    /// Blanks lines whose first non-whitespace characters open or continue a comment. Only
    /// comment-<i>only</i> lines are blanked: a trailing comment on a code line is left in place,
    /// so hiding a violation behind <c>//</c> mid-line trips the guard rather than evading it.
    /// </summary>
    private static string BlankCommentLines(string text)
    {
        var lines = text.Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (
                trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal)
                || trimmed.StartsWith('*')
            )
            {
                lines[i] = string.Empty;
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Keeps comment text and blanks everything else, newline positions included, so a reported
    /// index still resolves to the line the reader sees in the editor. Trailing comments on code
    /// lines are kept as well — a violation does not stop being one for sharing a line with code.
    /// </summary>
    public static string CommentCanvas(string text)
    {
        var kept = text.Select(character => character == '\n' ? '\n' : ' ').ToArray();

        foreach (Match match in CommentOrStringLiteral.Matches(text))
        {
            if (!IsComment(match.Value))
            {
                continue;
            }

            for (var offset = 0; offset < match.Length; offset++)
            {
                kept[match.Index + offset] = text[match.Index + offset];
            }
        }

        return new string(kept);
    }

    private static bool IsComment(string matched) =>
        matched.StartsWith("//", StringComparison.Ordinal)
        || matched.StartsWith("/*", StringComparison.Ordinal);
}
