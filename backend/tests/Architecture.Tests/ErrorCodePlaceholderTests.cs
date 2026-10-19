using System.Text.RegularExpressions;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// A catalog key existing is not the same as a catalog key rendering. <c>ErrorCodeCatalogKeyTests</c>
/// proves every code has a sentence and every sentence has a code, but a sentence that interpolates
/// <c>{{max}}</c> when nothing supplies <c>max</c> passes that guard and still reaches the user with
/// a literal <c>{{max}}</c> in it. Neither frontend guard can see this either: the completeness test
/// compares key sets rather than message text, and the dead-key test allowlists the whole
/// <c>errors.</c> prefix because those keys are reached by runtime code.
/// <para>
/// The params each code carries are already written down twice — in the Params column of
/// <c>docs/ERROR-CODES.md</c> and in the <c>Params:</c> summary comments on <c>ErrorCodes</c>. These
/// guards hold the catalog to the first and the two manifests to each other, so silencing a failure
/// by editing a manifest takes matching edits in two files in two languages rather than one.
/// </para>
/// <para>
/// Note what they deliberately do not claim: that an emitter supplies the param. Grounding that
/// would mean deriving params from three unrelated producers — literal dictionaries at the
/// <c>DomainError</c> sites, the forwarded-placeholder allowlist in <c>ValidationFailureMapper</c>,
/// and <c>IdentityErrorMapper</c> recovering values out of Identity's English. These guards check
/// the contract as written, which is why <c>validation.too_short</c> passes with no producer at all.
/// </para>
/// </summary>
public class ErrorCodePlaceholderTests
{
    private const string ReferencePath = "docs/ERROR-CODES.md";
    private const string ErrorsSection = "errors";
    private const string ErrorCodesPath = "src/Core.Domain/Errors/ErrorCodes.cs";
    private const string CodeColumn = "Code";
    private const string ParamsColumn = "Params";
    private const string DeclarationPrefix = "public const string ";

    /// <summary>An i18next interpolation, e.g. <c>{{missingLabels}}</c>.</summary>
    private static readonly Regex Placeholder = new(
        @"\{\{\s*(?<name>[^}\s]+?)\s*\}\}",
        RegexOptions.Compiled
    );

    /// <summary>An inline code span, how the reference writes both codes and param names.</summary>
    private static readonly Regex InlineCodeSpan = new(@"`(?<span>[^`]+)`", RegexOptions.Compiled);

    /// <summary>A <c>&lt;c&gt;</c> span, how a summary comment writes a param name.</summary>
    private static readonly Regex CommentCodeSpan = new(
        @"<c>(?<name>[^<]+)</c>",
        RegexOptions.Compiled
    );

    private static readonly Regex ConstantDeclaration = new(
        @"^public const string \w+ = ""(?<code>[^""]+)"";",
        RegexOptions.Compiled
    );

    /// <summary>
    /// A param name, stripped of the type annotation the reference sometimes appends —
    /// <c>`max` (int)</c> is the param <c>max</c>. Also reduces the one irregular cell,
    /// <c>`errors: [{field, code, params}]`</c>, to the param it actually names.
    /// </summary>
    private static readonly Regex LeadingIdentifier = new(
        @"^[A-Za-z_][A-Za-z0-9_]*",
        RegexOptions.Compiled
    );

    [Theory]
    [InlineData("en-US.json")]
    [InlineData("pt-BR.json")]
    public void Every_catalog_placeholder_is_documented_as_a_param(string catalogFile)
    {
        var documented = DocumentedParams();

        var interpolating = LocaleCatalogFile
            .Messages(catalogFile, ErrorsSection)
            .Select(message => (Code: message.Key, Placeholders: PlaceholdersIn(message.Value)))
            .Where(message => message.Placeholders.Count > 0)
            .OrderBy(message => message.Code, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            interpolating.Length > 0,
            $"""
            {nameof(Every_catalog_placeholder_is_documented_as_a_param)}: no message under `errors.`
            in {catalogFile} was found to interpolate anything, which cannot be right — the catalog
            carries interpolating sentences today. The placeholder pattern no longer matches the
            catalog, so this guard is passing without checking anything.
            """
        );

        var violations = new List<string>();

        foreach (var (code, placeholders) in interpolating)
        {
            if (!documented.TryGetValue(code, out var declared))
            {
                violations.Add(
                    $"{code}: interpolates {Format(placeholders)} but has no row with a "
                        + $"{ParamsColumn} column in {ReferencePath}"
                );
                continue;
            }

            var undeclared = placeholders
                .Where(placeholder => !declared.Contains(placeholder))
                .OrderBy(placeholder => placeholder, StringComparer.Ordinal)
                .ToArray();

            if (undeclared.Length > 0)
            {
                violations.Add(
                    $"{code}: interpolates {Format(undeclared)}, which the {ParamsColumn} column "
                        + $"does not list (it lists {Format(declared)})"
                );
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
            {nameof(Every_catalog_placeholder_is_documented_as_a_param)}: {violations.Count}
            message(s) in {catalogFile} interpolate a value that {ReferencePath} does not promise.
            i18next renders an unsupplied placeholder literally, so the reader is shown the raw
            placeholder text where a value belongs. Either the sentence should not interpolate it,
            or the code does carry it and the reference needs the row.
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    [Fact]
    public void Every_params_comment_matches_the_reference_table()
    {
        var documented = DocumentedParams();
        var commented = CommentedParams();

        Assert.True(
            commented.Count > 0,
            $"""
            {nameof(Every_params_comment_matches_the_reference_table)}: no `Params:` summary comment
            was found in {ErrorCodesPath}. Those comments exist, so the parser has drifted from how
            they are written and this guard is passing without comparing anything.
            """
        );

        var violations = new List<string>();

        foreach (var (code, names) in commented.OrderBy(entry => entry.Key, StringComparer.Ordinal))
        {
            if (!documented.TryGetValue(code, out var declared))
            {
                violations.Add(
                    $"{code}: {ErrorCodesPath} documents {Format(names)} but the code has no row "
                        + $"with a {ParamsColumn} column in {ReferencePath}"
                );
                continue;
            }

            if (!names.SetEquals(declared))
            {
                violations.Add(
                    $"{code}: {ErrorCodesPath} says {Format(names)}, {ReferencePath} says "
                        + $"{Format(declared)}"
                );
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
            {nameof(Every_params_comment_matches_the_reference_table)}: {violations.Count} code(s)
            whose two written records of their params disagree. The disagreement is the finding: one
            of them is telling the frontend to expect a value the backend does not send, or hiding
            one it does. Fix whichever is wrong — and note that a param documented in only one place
            is how a catalog placeholder gets waved through by the guard above.
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    /// <summary>
    /// The reverse of <see cref="Every_params_comment_matches_the_reference_table"/>: that guard
    /// only reaches a code once someone writes the comment, so on its own it leaves the reference
    /// as the single record for every code nobody got around to. This one closes that, and it is
    /// also what makes an unreadable comment audible — a comment the parser cannot attribute is
    /// indistinguishable from no comment, so without this the coverage silently regresses.
    /// </summary>
    [Fact]
    public void Every_code_with_documented_params_has_a_params_comment()
    {
        var required = DocumentedParams()
            .Where(entry => entry.Value.Count > 0)
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            required.Length > 0,
            $"""
            {nameof(Every_code_with_documented_params_has_a_params_comment)}: no row in
            {ReferencePath} names a param, which cannot be right — the reference documents params
            today. The {ParamsColumn} column is no longer being read, so this guard is passing
            without checking anything.
            """
        );

        var commented = CommentedParams();

        var missing = required
            .Where(entry => !commented.ContainsKey(entry.Key))
            .Select(entry => $"{entry.Key}: {ReferencePath} lists {Format(entry.Value)}")
            .ToArray();

        Assert.True(
            missing.Length == 0,
            $"""
            {nameof(Every_code_with_documented_params_has_a_params_comment)}: {missing.Length}
            code(s) whose params are written down in one place only. A lone doc row is a single
            point of truth, and blanking its cell is the cheapest way to silence
            {nameof(Every_catalog_placeholder_is_documented_as_a_param)} — with a summary comment as
            well, waving a param through takes matching edits in two files in two languages.
            Give each of these a `Params:` summary comment naming the same params.
            Missing:
            {string.Join("\n", missing)}
            """
        );
    }

    /// <summary>
    /// The params each code is documented to carry, read off the reference's markdown tables.
    /// <para>
    /// The <c>Params</c> column is located by header name rather than by position, because the
    /// <c>redemption.failure</c> table has no such column and holds the enum name where the others
    /// hold params — a positional read would report <c>TokenDecryptionFailed</c> as a param name.
    /// Codes documented only in prose have no row and are simply absent, which is what lets the
    /// retired <c>auth.*</c> codes stay documented as prose.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlySet<string>> DocumentedParams()
    {
        var documented = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        var codeIndex = -1;
        var paramsIndex = -1;

        foreach (var line in File.ReadAllLines(SourcePaths.RepositoryFile(ReferencePath)))
        {
            var trimmed = line.Trim();

            if (!trimmed.StartsWith('|'))
            {
                codeIndex = paramsIndex = -1;
                continue;
            }

            var cells = Cells(trimmed);

            if (IsSeparator(cells))
            {
                continue;
            }

            if (Array.IndexOf(cells, CodeColumn) >= 0)
            {
                codeIndex = Array.IndexOf(cells, CodeColumn);
                paramsIndex = Array.IndexOf(cells, ParamsColumn);
                continue;
            }

            if (paramsIndex < 0 || codeIndex >= cells.Length || paramsIndex >= cells.Length)
            {
                continue;
            }

            var code = InlineCodeSpan.Match(cells[codeIndex]);
            if (code.Success)
            {
                documented[code.Groups["span"].Value] = ParamNames(cells[paramsIndex]);
            }
        }

        if (documented.Count == 0)
        {
            throw new InvalidOperationException(
                $"No table rows were parsed out of {ReferencePath}. The reference is a table of "
                    + "error codes, so this means the format moved and the guards reading it would "
                    + "otherwise pass vacuously."
            );
        }

        return documented;
    }

    /// <summary>
    /// The params each code is documented to carry by its <c>Params:</c> summary comment on
    /// <see cref="Core.Domain.Errors.ErrorCodes"/>. Read from source rather than by reflection
    /// because comments do not survive compilation.
    /// <para>
    /// A declaration is accumulated up to its semicolon rather than matched a line at a time. The
    /// longest code names push the single-line form past the formatter's print width, so the
    /// literal lands on the following line and a line-at-a-time read finds no declaration there at
    /// all — and finding none is silent, because a constant whose comment cannot be read is skipped
    /// rather than reported. Any other line still clears the block, so a comment cannot carry onto
    /// a declaration it does not belong to.
    /// </para>
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlySet<string>> CommentedParams()
    {
        var commented = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);
        var block = new List<string>();
        var declaration = string.Empty;

        foreach (var line in File.ReadAllLines(SourcePaths.BackendFile(ErrorCodesPath)))
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("///", StringComparison.Ordinal))
            {
                block.Add(trimmed);
                continue;
            }

            if (
                declaration.Length == 0
                && !trimmed.StartsWith(DeclarationPrefix, StringComparison.Ordinal)
            )
            {
                block.Clear();
                continue;
            }

            declaration = declaration.Length == 0 ? trimmed : $"{declaration} {trimmed}";

            if (!trimmed.EndsWith(';'))
            {
                continue;
            }

            var match = ConstantDeclaration.Match(declaration);
            if (match.Success && block.Count > 0)
            {
                var names = ParamsInComment(block);
                if (names.Count > 0)
                {
                    commented[match.Groups["code"].Value] = names;
                }
            }

            declaration = string.Empty;
            block.Clear();
        }

        return commented;
    }

    /// <summary>
    /// The param names in a summary comment, or none when it documents no params. Only the text
    /// after <c>Params:</c> counts — the rest of a summary is prose and may name other things.
    /// </summary>
    private static IReadOnlySet<string> ParamsInComment(IEnumerable<string> block)
    {
        var summary = string.Join(" ", block);
        var marker = summary.IndexOf("Params:", StringComparison.Ordinal);

        if (marker < 0)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return CommentCodeSpan
            .Matches(summary[marker..])
            .Select(match => match.Groups["name"].Value.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// The param names in a <c>Params</c> cell. Each is an inline code span, sometimes trailed by a
    /// type — <c>`missingLabels` (string[])</c>. A cell holding an em dash carries no spans and so
    /// yields nothing, which is the same answer as "documents no params".
    /// </summary>
    private static IReadOnlySet<string> ParamNames(string cell) =>
        InlineCodeSpan
            .Matches(cell)
            .Select(match => LeadingIdentifier.Match(match.Groups["span"].Value))
            .Where(identifier => identifier.Success)
            .Select(identifier => identifier.Value)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> PlaceholdersIn(string message) =>
        Placeholder
            .Matches(message)
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static string[] Cells(string row) =>
        row.Trim('|').Split('|').Select(cell => cell.Trim()).ToArray();

    private static bool IsSeparator(string[] cells) =>
        cells.All(cell => cell.Length > 0 && cell.Trim(':', '-').Length == 0);

    private static string Format(IEnumerable<string> names)
    {
        var ordered = names.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        return ordered.Length == 0 ? "no params" : string.Join(", ", ordered);
    }
}
