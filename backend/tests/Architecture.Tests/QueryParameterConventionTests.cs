using System.Reflection;
using System.Runtime.CompilerServices;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// Query parameters travel in an <c>[AsParameters]</c> record, one per endpoint — even where the
/// endpoint reads a single parameter besides its <c>CancellationToken</c>. Bound loosely they have
/// nowhere to hang a validator and no name a per-field failure can point at, which is the gap that
/// left the query surface answering in a different shape from every request body. These tests are
/// what keeps the rule from being re-derived at review time.
/// </summary>
public class QueryParameterConventionTests
{
    // Compared by name rather than typeof(IResult): this project is Microsoft.NET.Sdk and does not
    // declare a FrameworkReference on Microsoft.AspNetCore.App.
    private const string ResultInterface = "Microsoft.AspNetCore.Http.IResult";
    private const string AsParameters = "Microsoft.AspNetCore.Http.AsParametersAttribute";

    /// <summary>
    /// A <c>string</c> handler parameter is always a loose query value: route templates in this app
    /// bind <c>{id:guid}</c> directly and a string segment only through an <c>[AsParameters]</c>
    /// record, services arrive as their own interfaces, and a body arrives as its contract. So this is the regression guard — adding a query parameter the old
    /// way trips it and the failure message says where the parameter belongs instead.
    /// </summary>
    [Fact]
    public void Endpoint_handlers_do_not_bind_a_loose_query_parameter()
    {
        var failing = (
            from handler in DiscoverHandlers()
            from parameter in handler.GetParameters()
            where parameter.ParameterType == typeof(string)
            select $"{Describe(handler)}: parameter '{parameter.Name}' binds a bare string"
        ).ToList();

        Assert.True(
            failing.Count == 0,
            $"""
            {nameof(
                Endpoint_handlers_do_not_bind_a_loose_query_parameter
            )}: {failing.Count} handler parameter(s) bind a query value directly.
            Move them into an [AsParameters] record under
            Presentation.WebApi/Endpoints/<Feature>/Contracts/<Name>Query.cs, keeping the property
            names the query string already uses, and take the record as one parameter instead.
            Violations:
            {string.Join("\n", failing)}
            """
        );
    }

    /// <summary>
    /// The other half of the same tell: a defaulted parameter is how a loose optional filter gets
    /// declared. <c>CancellationToken</c> is exempt because a defaulted token is an ordinary .NET
    /// idiom and is never a query value.
    /// </summary>
    [Fact]
    public void Endpoint_handlers_do_not_default_a_bound_parameter()
    {
        var failing = (
            from handler in DiscoverHandlers()
            from parameter in handler.GetParameters()
            where parameter.HasDefaultValue && parameter.ParameterType != typeof(CancellationToken)
            select $"{Describe(handler)}: parameter '{parameter.Name}' declares a default"
        ).ToList();

        Assert.True(
            failing.Count == 0,
            $"""
            {nameof(
                Endpoint_handlers_do_not_default_a_bound_parameter
            )}: {failing.Count} handler parameter(s) declare a default value.
            An optional query value is optional because its record property is nullable, not because
            the handler defaults it. Move it into the endpoint's [AsParameters] record.
            Violations:
            {string.Join("\n", failing)}
            """
        );
    }

    /// <summary>
    /// Keeps the contracts honest in the other direction: a record left behind after its endpoint
    /// changed is dead weight that still reads like the live shape of a request.
    /// </summary>
    [Fact]
    public void Every_query_contract_is_bound_by_a_handler()
    {
        var bound = DiscoverHandlers()
            .SelectMany(handler => handler.GetParameters())
            .Where(parameter =>
                parameter
                    .GetCustomAttributes()
                    .Any(attribute => attribute.GetType().FullName == AsParameters)
            )
            .Select(parameter => parameter.ParameterType)
            .ToHashSet();

        var orphans = DiscoverQueryContracts()
            .Where(contract => !bound.Contains(contract))
            .Select(contract => contract.FullName ?? contract.Name)
            .ToList();

        Assert.True(
            orphans.Count == 0,
            $"""
            {nameof(
                Every_query_contract_is_bound_by_a_handler
            )}: {orphans.Count} query contract(s) are not bound by any handler.
            Bind them with [AsParameters] on the endpoint that reads them, or delete them.
            Violations:
            {string.Join("\n", orphans)}
            """
        );
    }

    /// <summary>
    /// Guards both directions of the discovery: an empty set of either would make the checks above
    /// pass without looking at anything.
    /// </summary>
    [Fact]
    public void The_scan_actually_finds_handlers_and_query_contracts()
    {
        Assert.True(
            DiscoverHandlers().Count > 10,
            "Expected the presentation assembly to declare endpoint handlers returning IResult."
        );

        Assert.True(
            DiscoverQueryContracts().Count > 5,
            "Expected a query contract per endpoint that reads the query string."
        );
    }

    /// <summary>
    /// An endpoint handler is a method that answers with an <c>IResult</c> and is declared on a type
    /// in an <c>Endpoints</c> namespace. The registrars themselves answer with an
    /// <c>IEndpointRouteBuilder</c>, so they are not swept up.
    /// </summary>
    private static IReadOnlyList<MethodInfo> DiscoverHandlers() =>
        ArchitectureAssemblies
            .Presentation.GetTypes()
            .Where(type =>
                type.IsClass
                && type.Namespace?.Contains(".Endpoints.", StringComparison.Ordinal) == true
                && !IsCompilerGenerated(type)
            )
            .SelectMany(type =>
                type.GetMethods(
                    BindingFlags.Public
                        | BindingFlags.NonPublic
                        | BindingFlags.Static
                        | BindingFlags.Instance
                        | BindingFlags.DeclaredOnly
                )
            )
            .Where(method =>
                !IsCompilerGenerated(method)
                && method.ReturnType.FullName?.Contains(ResultInterface, StringComparison.Ordinal)
                    == true
            )
            .OrderBy(
                method => $"{method.DeclaringType?.FullName}.{method.Name}",
                StringComparer.Ordinal
            )
            .ToList();

    private static IReadOnlyList<Type> DiscoverQueryContracts() =>
        ArchitectureAssemblies
            .Presentation.GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false }
                && !IsCompilerGenerated(type)
                && type.Namespace?.EndsWith(".Contracts", StringComparison.Ordinal) == true
                && type.Name.EndsWith("Query", StringComparison.Ordinal)
            )
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();

    private static string Describe(MethodInfo handler) =>
        $"{handler.DeclaringType?.Name}.{handler.Name}";

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.GetCustomAttribute<CompilerGeneratedAttribute>() is not null;
}
