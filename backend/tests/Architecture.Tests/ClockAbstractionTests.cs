using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// Guards the single-clock rule: nothing under <c>src/</c> reads the ambient clock. Production code
/// takes <see cref="TimeProvider"/> and the domain takes an explicit <c>now</c>, so every
/// time-dependent behaviour is testable without sleeping. The registration itself is covered by
/// <c>CoreApplicationServiceCollectionExtensionsTests</c>.
/// </summary>
/// <remarks>
/// This reads source rather than IL. Scanning compiled assemblies looked tidier but is not sound
/// under coverage collection: coverlet instruments the assemblies in place and injects its own
/// tracker, whose <c>DateTime.UtcNow</c> reads are indistinguishable from ours in the metadata
/// tables — green locally, red in CI. Source is the thing the rule is actually about.
/// </remarks>
public class ClockAbstractionTests
{
    /// <summary>Clock accessors that must never appear in production code.</summary>
    private static readonly Regex ForbiddenAccessor = new(
        @"\b(DateTime|DateTimeOffset)\s*\.\s*(UtcNow|Now|Today)\b",
        RegexOptions.Compiled
    );

    /// <summary>EF-generated migrations are not hand-written code and are exempt.</summary>
    private const string GeneratedDirectory = "Migrations";

    [Fact]
    public void Core_Domain_never_reads_the_ambient_clock()
    {
        var offenders = FindForbiddenClockReads(Path.Combine(SrcRoot(), "Core.Domain"));

        Assert.True(
            offenders.Length == 0,
            "Domain entities must take an explicit 'now' parameter instead of reading the clock. "
                + $"Found:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
        );
    }

    [Fact]
    public void Production_code_reads_the_clock_only_through_TimeProvider()
    {
        var offenders = FindForbiddenClockReads(SrcRoot());

        Assert.True(
            offenders.Length == 0,
            "Inject TimeProvider and call GetUtcNow() instead. "
                + $"Found:{Environment.NewLine}{string.Join(Environment.NewLine, offenders)}"
        );
    }

    /// <summary>
    /// Fails if the scan finds nothing to read, so a wrong path cannot make the rules above pass
    /// vacuously.
    /// </summary>
    [Fact]
    public void The_scan_actually_covers_the_production_source()
    {
        var files = EnumerateProductionSources(SrcRoot()).ToArray();

        Assert.True(
            files.Length > 100,
            $"Expected to scan the whole of src/, found only {files.Length} file(s) under {SrcRoot()}"
        );
    }

    private static string[] FindForbiddenClockReads(string root)
    {
        var offenders = new List<string>();

        foreach (var file in EnumerateProductionSources(root))
        {
            var code = StripCommentsAndStrings(File.ReadAllText(file));

            foreach (var line in code.Split('\n').Index())
            {
                if (ForbiddenAccessor.IsMatch(line.Item))
                {
                    offenders.Add(
                        $"{Path.GetFileName(file)}({line.Index + 1}): {line.Item.Trim()}"
                    );
                }
            }
        }

        return [.. offenders.Order()];
    }

    private static IEnumerable<string> EnumerateProductionSources(string root) =>
        Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Split(Path.DirectorySeparatorChar).Contains(GeneratedDirectory))
            // bin/obj hold generated and copied sources, not the code under review.
            .Where(f =>
                !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
            );

    /// <summary>
    /// Blanks out comments and string literals so that documenting the rule — as several files here
    /// do — does not read as breaking it. Newlines are preserved to keep line numbers honest.
    /// </summary>
    private static string StripCommentsAndStrings(string code)
    {
        return Regex.Replace(
            code,
            """
            //.*?$|/\*.*?\*/|"(?:\\.|[^"\\])*"|@"(?:[^"]|"")*"
            """,
            match => Blank(match.Value),
            RegexOptions.Singleline | RegexOptions.Multiline
        );

        static string Blank(string matched) =>
            new([.. matched.Select(c => c == '\n' ? '\n' : ' ')]);
    }

    /// <summary>
    /// Resolves <c>backend/src</c> from this file's compile-time path: the arch tests live at
    /// <c>backend/tests/Architecture.Tests/</c>, so <c>src</c> is two levels up.
    /// </summary>
    private static string SrcRoot([CallerFilePath] string thisFile = "")
    {
        var testProject = Path.GetDirectoryName(thisFile)!;
        return Path.GetFullPath(Path.Combine(testProject, "..", "..", "src"));
    }
}
