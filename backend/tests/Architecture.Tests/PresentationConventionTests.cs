using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using NetArchTest.Rules;

namespace PointLurker.Architecture.Tests;

public class PresentationConventionTests
{
    private const string PresentationProject = "Presentation.WebApi";
    private const string EndpointsFolder = "Endpoints";

    /// <summary>
    /// Ways an error message or an error code can be written into a response by hand instead of
    /// travelling as an <c>ErrorCodes.*</c> constant the frontend catalog can resolve. Each entry
    /// carries the sentence printed when it matches, so a failure says what to do instead.
    /// </summary>
    private static readonly (Regex Pattern, string Explanation)[] ForbiddenErrorText =
    [
        (
            new Regex(
                @"\b(?:Typed)?Results\.(?:BadRequest|Conflict|NotFound|UnprocessableEntity|Text|Content|Json)\s*\(\s*(?:new\s*\{\s*\w+\s*=\s*)?[@$]*""",
                RegexOptions.Compiled
            ),
            "error text passed straight to a Results helper — return Results.Problem(ProblemDetailsFactory.Create(new DomainError(ErrorCodes.X), status, title)) so the response carries code + params"
        ),
        (
            new Regex(@"\b[Dd]etail\s*[:=]\s*[@$]*""", RegexOptions.Compiled),
            "hand-written ProblemDetails detail string — the frontend renders the user-facing message from code + params, so detail must not carry it"
        ),
        (
            new Regex(@"""[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+""", RegexOptions.Compiled),
            "SCREAMING_SNAKE error code literal — this is the second taxonomy the layer used to switch on; switch on ErrorCodes.* instead"
        ),
    ];

    /// <summary>A string literal shaped like an error code: lowercase dot-separated snake_case.</summary>
    private static readonly Regex CodeShapedLiteral = new(
        @"""(?<code>[a-z][a-z0-9_]*(?:\.[a-z][a-z0-9_]*)+)""",
        RegexOptions.Compiled
    );

    /// <summary>
    /// A <c>new DomainError(...)</c> call and its first argument. The capture stops at the first
    /// <c>,</c> or <c>)</c>, which is exact for an identifier, a member access or a literal; an
    /// argument that contains either character is a call or a compound expression, so it fails the
    /// check whether the capture is complete or truncated.
    /// </summary>
    private static readonly Regex DomainErrorConstruction = new(
        @"new\s+DomainError\s*\(\s*(?<argument>[^,)]*)",
        RegexOptions.Compiled
    );

    /// <summary>
    /// The only sanctioned <c>new DomainError(...)</c> arguments that do not name a constant: both
    /// re-emit a code produced upstream from <c>ErrorCodes.*</c>, so naming a constant here would
    /// mean re-deciding the code the mapper was handed. Every entry must match something — a stale
    /// one fails <see cref="Every_DomainError_names_its_code_from_ErrorCodes"/>.
    /// </summary>
    private static readonly (string File, string Expression)[] ForwardedCodes =
    [
        // ErrorMapping projects an IApplicationError, which already carries its canonical code.
        ("Errors/ErrorMapping.cs", "error.Code"),
        // GlobalExceptionHandler.Describe picked the code from ErrorCodes.* one call earlier.
        ("Errors/GlobalExceptionHandler.cs", "code"),
    ];

    /// <summary>
    /// Every error code emitted by this layer must come from an <c>ErrorCodes.*</c> constant, and
    /// every user-facing message must be resolved by the frontend from that code — so no error code
    /// and no error message may be written as a literal in Presentation.WebApi.
    /// <para>
    /// This test fails the build; there is no suppression and no skip. Where the previous version
    /// returned early — source directory not found — it now throws, because a guard that scans
    /// nothing reports no violations and reads as a pass. The path comes from
    /// <see cref="SourcePaths"/> (an MSBuild-baked constant), not from a directory walk that can
    /// quietly resolve to nothing.
    /// </para>
    /// <para>
    /// Only comment-<i>only</i> lines are exempt (see <see cref="SourceScan"/>): documentation may
    /// quote a code, code may not.
    /// </para>
    /// </summary>
    [Fact]
    public void No_inline_error_string_literals_in_presentation_layer()
    {
        var violations = new List<string>();

        foreach (
            var file in SourceScan.CSharpFiles(SourcePaths.ProjectSourceRoot(PresentationProject))
        )
        {
            foreach (var (pattern, explanation) in ForbiddenErrorText)
            {
                foreach (Match match in pattern.Matches(file.Text))
                {
                    violations.Add(Describe(file, match.Index, explanation));
                }
            }

            foreach (Match match in CodeShapedLiteral.Matches(file.Text))
            {
                var code = match.Groups["code"].Value;

                // A dotted lowercase literal is only an error code if it says so: either it is one
                // verbatim, or its leading segment names a declared namespace, which is how a typo
                // (`stores.not_fund`) is caught without flagging an unrelated literal such as a
                // file name.
                var explanation =
                    ErrorCodeCatalog.Codes.Contains(code)
                        ? $"inline copy of the ErrorCodes constant for '{code}' — reference the constant"
                    : ErrorCodeCatalog.Namespaces.Contains(code.Split('.')[0])
                        ? $"'{code}' is shaped like an error code in a declared namespace but is not a declared code — add it to ErrorCodes and reference the constant"
                    : null;

                if (explanation is not null)
                {
                    violations.Add(Describe(file, match.Index, explanation));
                }
            }
        }

        // A line can break more than one rule — Results.Conflict(new { detail = "..." }) breaks two
        // — and each match is worth naming once.
        var distinct = violations.Distinct().ToArray();

        Assert.True(
            distinct.Length == 0,
            $"""
            {nameof(
                No_inline_error_string_literals_in_presentation_layer
            )}: Found {distinct.Length} inline error literal(s).
            Violations:
            {string.Join("\n", distinct)}
            """
        );
    }

    /// <summary>
    /// The code carried by every <c>DomainError</c> this layer constructs must be named as an
    /// <c>ErrorCodes.*</c> constant, so <c>docs/ERROR-CODES.md</c> and the frontend catalog stay the
    /// only places a code is defined. The two exceptions in <see cref="ForwardedCodes"/> forward a
    /// code that Core.Application or Core.Domain already built from the same constants; every other
    /// argument — literal, interpolation, concatenation, call — is a new code smuggled in.
    /// </summary>
    [Fact]
    public void Every_DomainError_names_its_code_from_ErrorCodes()
    {
        var violations = new List<string>();
        var unusedForwards = ForwardedCodes.ToHashSet();

        foreach (
            var file in SourceScan.CSharpFiles(SourcePaths.ProjectSourceRoot(PresentationProject))
        )
        {
            foreach (Match match in DomainErrorConstruction.Matches(file.Text))
            {
                var argument = match.Groups["argument"].Value.Trim();

                if (argument.StartsWith("ErrorCodes.", StringComparison.Ordinal))
                {
                    continue;
                }

                if (ForwardedCodes.Contains((file.RelativePath, argument)))
                {
                    unusedForwards.Remove((file.RelativePath, argument));
                    continue;
                }

                violations.Add(
                    Describe(
                        file,
                        match.Index,
                        $"code argument is `{argument}` — name an ErrorCodes.* constant, or add a reviewed entry to ForwardedCodes if it forwards a code built upstream"
                    )
                );
            }
        }

        Assert.True(
            violations.Count == 0 && unusedForwards.Count == 0,
            $"""
            {nameof(Every_DomainError_names_its_code_from_ErrorCodes)}: {violations.Count} unnamed
            code argument(s), {unusedForwards.Count} stale ForwardedCodes entr(y/ies).
            Violations:
            {string.Join("\n", violations)}
            Stale exceptions (the forward is gone — delete the entry):
            {string.Join("\n", unusedForwards.Select(f => $"{f.File}: {f.Expression}"))}
            """
        );
    }

    private static string Describe(ScannedFile file, int index, string explanation) =>
        $"{file.RelativePath}:{file.LineAt(index)}: {explanation}{Environment.NewLine}    {file.ExcerptAt(index)}";

    /// <summary>
    /// Endpoint handlers must not resolve the caller with <c>UserManager.GetUserAsync(principal)</c>.
    /// The owner id comes from the bearer principal (<c>ClaimsPrincipal.GetUserId()</c>, zero
    /// round-trips); the owner entity comes from <c>ISingleUserProvider</c>, the same port the
    /// Identity use cases already use. Eight handlers once repeated the identical
    /// fetch-then-null-check preamble — one database round-trip per request, guarding a null branch
    /// that a group-level <c>RequireAuthorization()</c> made unreachable. This keeps it from growing
    /// back. <c>UserManager&lt;&gt;</c> itself stays legal here: the anonymous bootstrap gate counts
    /// rows with it.
    /// </summary>
    [Fact]
    public void Endpoint_handlers_do_not_resolve_the_current_user_via_UserManager()
    {
        // Scanned from the project root and filtered, so reported paths stay project-relative.
        var handlers = SourceScan
            .CSharpFiles(SourcePaths.ProjectSourceRoot(PresentationProject))
            .Where(f => f.RelativePath.StartsWith($"{EndpointsFolder}/", StringComparison.Ordinal))
            .ToArray();

        // Asserted, not assumed: a renamed or emptied folder must fail the test rather than reduce
        // it to scanning nothing, which would report no violations and read as a pass.
        Assert.True(
            handlers.Length > 0,
            $"No handler sources found under {EndpointsFolder}/ — the guard would pass vacuously. Has the folder moved?"
        );

        var forbidden = new Regex(@"\bGetUserAsync\s*\(", RegexOptions.Compiled);
        var violations = new List<string>();

        foreach (var file in handlers)
        {
            foreach (Match match in forbidden.Matches(file.Text))
            {
                violations.Add(Describe(file, match.Index, "per-handler current-user lookup"));
            }
        }

        Assert.True(
            violations.Count == 0,
            $"""
            {nameof(
                Endpoint_handlers_do_not_resolve_the_current_user_via_UserManager
            )}: Found {violations.Count} per-handler current-user lookup(s).
            Read the owner id from the bearer principal (ClaimsPrincipal.GetUserId()) or load the owner entity via ISingleUserProvider — never UserManager.GetUserAsync in a handler.
            Violations:
            {string.Join("\n", violations)}
            """
        );
    }

    [Fact]
    public void Presentation_must_not_reference_repository_implementations()
    {
        var result = Types
            .InAssembly(ArchitectureAssemblies.Presentation)
            .Should()
            .NotHaveDependencyOn("PointLurker.Infrastructure.Database.Repositories")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            ArchAssert.Format(
                nameof(Presentation_must_not_reference_repository_implementations),
                result
            )
        );
    }

    /// <summary>
    /// Anything that registers HTTP routes must live under an <c>Endpoints/</c> folder, so the
    /// project layout keeps stating plainly what is transport surface and what is not.
    /// Detected behaviourally — a static method whose first parameter is
    /// <c>IEndpointRouteBuilder</c> — rather than by the <c>*Endpoints</c> name suffix, so that
    /// middleware such as <c>SuppressedIdentityEndpoints</c> (which extends
    /// <c>IApplicationBuilder</c>) is excluded without an allowlist, and a route registrar that
    /// gets named something else entirely is still caught.
    /// </summary>
    [Fact]
    public void Endpoint_registrars_live_under_the_Endpoints_namespace()
    {
        // Compared by name rather than typeof(IEndpointRouteBuilder): this project is
        // Microsoft.NET.Sdk and does not declare a FrameworkReference on Microsoft.AspNetCore.App.
        const string RouteBuilder = "Microsoft.AspNetCore.Routing.IEndpointRouteBuilder";

        var failing = new List<string>();

        foreach (var t in ArchitectureAssemblies.Presentation.GetTypes())
        {
            if (!t.IsClass || IsCompilerGenerated(t))
            {
                continue;
            }

            var registersRoutes = t.GetMethods(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                )
                .Any(m =>
                    m.GetParameters() is [var first, ..]
                    && first.ParameterType.FullName == RouteBuilder
                );

            if (
                registersRoutes
                && t.Namespace?.Contains(".Endpoints.", StringComparison.Ordinal) != true
            )
            {
                failing.Add(t.FullName ?? t.Name);
            }
        }

        Assert.True(
            failing.Count == 0,
            $"""
            {nameof(
                Endpoint_registrars_live_under_the_Endpoints_namespace
            )}: {failing.Count} type(s) register HTTP routes from outside an Endpoints namespace.
            Move them under Presentation.WebApi/Endpoints/<Feature>/ and match the namespace.
            Violations:
            {string.Join("\n", failing)}
            """
        );
    }

    /// <summary>
    /// HTTP contracts live in one place per feature, <c>Endpoints/&lt;Feature&gt;/Contracts/</c>, so
    /// they stay nameable from tests and cannot drift back to being nested private records inside
    /// the endpoint class — which is what kept them untestable and let four different placement
    /// conventions coexist. <c>GetTypes()</c> returns nested and non-public types, so a regression
    /// is caught wherever it is declared.
    /// </summary>
    [Fact]
    public void Request_and_response_contracts_live_in_a_Contracts_namespace()
    {
        var failing = new List<string>();

        foreach (var t in ArchitectureAssemblies.Presentation.GetTypes())
        {
            if (!t.IsClass || IsCompilerGenerated(t))
            {
                continue;
            }

            var isContract =
                t.Name.EndsWith("Request", StringComparison.Ordinal)
                || t.Name.EndsWith("Response", StringComparison.Ordinal);

            if (isContract && t.Namespace?.EndsWith(".Contracts", StringComparison.Ordinal) != true)
            {
                failing.Add(t.FullName ?? t.Name);
            }
        }

        Assert.True(
            failing.Count == 0,
            $"""
            {nameof(
                Request_and_response_contracts_live_in_a_Contracts_namespace
            )}: {failing.Count} HTTP contract(s) declared outside a Contracts namespace.
            Give each one its own file under Presentation.WebApi/Endpoints/<Feature>/Contracts/ and
            match the namespace.
            Violations:
            {string.Join("\n", failing)}
            """
        );
    }

    [Fact]
    public void Non_abstract_classes_are_sealed_in_src_assemblies()
    {
        var failing = new List<string>();

        foreach (var asm in ArchitectureAssemblies.SourceAssemblies)
        {
            foreach (var t in asm.GetTypes())
            {
                if (!t.IsClass || t.IsAbstract || t.IsSealed)
                {
                    continue;
                }

                if (IsCompilerGenerated(t))
                {
                    continue;
                }

                if (InheritsFrom(t, typeof(DbContext)))
                {
                    continue;
                }

                if (InheritsFrom(t, typeof(Migration)) || InheritsFrom(t, typeof(ModelSnapshot)))
                {
                    continue;
                }

                if (t.Namespace?.Contains(".Migrations", StringComparison.Ordinal) == true)
                {
                    continue;
                }

                if (t.Namespace?.StartsWith("Refit", StringComparison.Ordinal) == true)
                {
                    continue;
                }

                if (
                    t.DeclaringType?.Namespace?.StartsWith("Refit", StringComparison.Ordinal)
                    == true
                )
                {
                    continue;
                }

                if (t.Name == "Program" || t.Name.StartsWith("<", StringComparison.Ordinal))
                {
                    continue;
                }

                failing.Add(t.FullName ?? t.Name);
            }
        }

        Assert.True(
            failing.Count == 0,
            $"{nameof(Non_abstract_classes_are_sealed_in_src_assemblies)}: {string.Join(", ", failing)}"
        );
    }

    [Fact]
    public void Async_methods_end_with_Async_suffix()
    {
        var failing = new List<string>();

        foreach (var asm in ArchitectureAssemblies.SourceAssemblies)
        {
            foreach (var t in asm.GetTypes())
            {
                if (IsCompilerGenerated(t))
                {
                    continue;
                }

                if (
                    t.Namespace?.StartsWith("Refit.Implementation", StringComparison.Ordinal)
                    == true
                )
                {
                    continue;
                }

                var methods = t.GetMethods(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Instance
                        | BindingFlags.Static
                        | BindingFlags.DeclaredOnly
                );

                foreach (var m in methods)
                {
                    if (m.GetCustomAttribute<AsyncStateMachineAttribute>() is null)
                    {
                        continue;
                    }

                    if (m.Name.StartsWith('<') || m.Name.Contains("$"))
                    {
                        continue;
                    }

                    if (!m.Name.EndsWith("Async", StringComparison.Ordinal))
                    {
                        failing.Add($"{t.FullName}.{m.Name}");
                    }
                }
            }
        }

        Assert.True(
            failing.Count == 0,
            $"{nameof(Async_methods_end_with_Async_suffix)}: {string.Join(", ", failing)}"
        );
    }

    private static bool IsCompilerGenerated(Type t)
    {
        if (t.GetCustomAttribute<CompilerGeneratedAttribute>() is not null)
        {
            return true;
        }
        if (t.Name.Contains('<', StringComparison.Ordinal))
        {
            return true;
        }
        return false;
    }

    private static bool InheritsFrom(Type t, Type baseType)
    {
        var cursor = t.BaseType;
        while (cursor is not null)
        {
            if (cursor == baseType)
            {
                return true;
            }
            cursor = cursor.BaseType;
        }
        return false;
    }
}
