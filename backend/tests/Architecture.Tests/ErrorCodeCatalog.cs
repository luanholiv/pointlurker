using System.Reflection;
using PointLurker.Core.Domain.Errors;

namespace PointLurker.Architecture.Tests;

/// <summary>
/// The declared error-code taxonomy, read off <see cref="ErrorCodes"/> by reflection so the guards
/// stay correct as codes are added — no second list to keep in step.
/// </summary>
internal static class ErrorCodeCatalog
{
    /// <summary>Every <c>string</c> constant declared under <see cref="ErrorCodes"/>.</summary>
    public static IReadOnlySet<string> Codes { get; } = Collect(typeof(ErrorCodes));

    /// <summary>
    /// The leading segment of every code — <c>stores</c>, <c>se_api</c>, <c>redemption</c>, … Used
    /// to tell a mistyped or hand-rolled error code apart from an unrelated dotted literal such as
    /// a file name.
    /// </summary>
    public static IReadOnlySet<string> Namespaces { get; } =
        Codes.Select(code => code.Split('.')[0]).ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> Collect(Type type)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field is { IsLiteral: true } && field.GetRawConstantValue() is string code)
            {
                codes.Add(code);
            }
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
        {
            codes.UnionWith(Collect(nested));
        }

        return codes;
    }
}
