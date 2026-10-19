namespace PointLurker.Core.Domain.Errors;

/// <summary>
/// Represents a domain-level error with a stable, language-free code and optional interpolation params.
/// Use <see cref="ErrorCodes"/> constants for the <paramref name="code"/> value.
/// The <paramref name="params"/> dictionary feeds frontend catalog interpolation — never include
/// PII such as passwords or full token values.
/// </summary>
/// <param name="Code">
/// Stable identifier in <c>&lt;bounded_context&gt;.&lt;snake_case&gt;</c> form
/// (e.g., <c>"users.register_already_exists"</c>).
/// </param>
/// <param name="Params">
/// Placeholders for the frontend message catalog. Keys are simple strings;
/// values may be <see langword="null" />, strings, numbers, or string arrays.
/// </param>
public sealed record DomainError(string Code, IReadOnlyDictionary<string, object?> Params)
{
    /// <summary>Creates a <see cref="DomainError"/> with no interpolation params.</summary>
    public DomainError(string code)
        : this(code, new Dictionary<string, object?>()) { }
}
