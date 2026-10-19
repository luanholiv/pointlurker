using System.Globalization;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Validation;

namespace PointLurker.Presentation.WebApi.Search;

/// <summary>
/// Reads one request's search parameters, rejecting anything it cannot interpret instead of
/// substituting a default. Filters arrive as raw strings so this layer — not the framework's model
/// binder — decides what a bad value produces: a <c>400</c> carrying the app-wide
/// <c>validation.failed</c> contract the frontend catalog can render.
/// </summary>
/// <remarks>
/// One instance parses one request. Every accessor returns a usable value even after a rejection, so
/// a handler reads all of its parameters and then bails once on <see cref="Failure" /> rather than
/// branching after each one. Every bad filter is reported in one answer, in the order the handler
/// read them, as a <see cref="FieldError" /> named by its query-string parameter.
/// </remarks>
internal sealed class SearchQuery
{
    private readonly List<FieldError> _errors = [];

    /// <summary>
    /// The <c>400</c> to return, or <see langword="null" /> when every parameter read so far
    /// parsed. Handlers check this once, after reading everything.
    /// </summary>
    public IResult? Failure =>
        _errors.Count == 0 ? null : Results.Problem(FieldErrorProblem.Create(_errors));

    /// <summary>
    /// Reads an enumerated parameter, falling back to <paramref name="fallback" /> when it is
    /// absent. Matching is by name and case-insensitive; an ordinal is not a name, so <c>"1"</c> is
    /// rejected rather than read as the second member.
    /// </summary>
    public TEnum EnumOr<TEnum>(string parameter, string? raw, TEnum fallback)
        where TEnum : struct, Enum => OptionalEnum<TEnum>(parameter, raw) ?? fallback;

    /// <summary>
    /// Reads an enumerated parameter that narrows nothing when absent.
    /// </summary>
    public TEnum? OptionalEnum<TEnum>(string parameter, string? raw)
        where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var names = Enum.GetNames<TEnum>();
        var match = Array.Find(
            names,
            name => string.Equals(name, raw, StringComparison.OrdinalIgnoreCase)
        );

        if (match is not null)
        {
            return Enum.Parse<TEnum>(match);
        }

        Fail(
            parameter,
            ErrorCodes.Search.InvalidEnumValue,
            new Dictionary<string, object?>
            {
                ["parameter"] = parameter,
                ["value"] = raw,
                ["allowed"] = names,
            }
        );
        return null;
    }

    public long? Int64(string parameter, string? raw) =>
        Parse<long>(
            parameter,
            raw,
            value =>
                long.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
                    ? parsed
                    : null
        );

    public int? Int32(string parameter, string? raw) =>
        Parse<int>(
            parameter,
            raw,
            value =>
                int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var parsed
                )
                    ? parsed
                    : null
        );

    /// <summary>
    /// Reads a boolean filter. Both spellings are accepted: the frontend's serializer writes
    /// <c>1</c> and <c>0</c>, and a hand-written URL is likelier to say <c>true</c>.
    /// </summary>
    public bool? Flag(string parameter, string? raw) =>
        Parse<bool>(
            parameter,
            raw,
            value =>
                value switch
                {
                    "1" => true,
                    "0" => false,
                    _ when bool.TryParse(value, out var parsed) => parsed,
                    _ => null,
                }
        );

    public DateTimeOffset? Timestamp(string parameter, string? raw) =>
        Parse<DateTimeOffset>(
            parameter,
            raw,
            value =>
                DateTimeOffset.TryParse(
                    value,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal,
                    out var parsed
                )
                    ? parsed
                    : null
        );

    public Guid? Identifier(string parameter, string? raw) =>
        Parse<Guid>(parameter, raw, value => Guid.TryParse(value, out var parsed) ? parsed : null);

    /// <summary>
    /// Splits a multi-valued filter. Empty segments are dropped, and a list left with nothing in it
    /// narrows nothing rather than matching nothing — <c>cat=</c> means "no category filter", not
    /// "items in no category".
    /// </summary>
    public static IReadOnlyList<string>? Csv(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var values = raw.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

        return values.Length == 0 ? null : values;
    }

    /// <summary>An absent or negative offset starts at the beginning; past the end simply returns nothing.</summary>
    public static int ClampOffset(int? offset) => offset is null or < 0 ? 0 : offset.Value;

    /// <summary>An absent limit takes the endpoint's default; anything else is clamped into range.</summary>
    public static int ClampLimit(int? limit, int fallback, int max) =>
        limit is null ? fallback : Math.Clamp(limit.Value, 1, max);

    private T? Parse<T>(string parameter, string? raw, Func<string, T?> parse)
        where T : struct
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var parsed = parse(raw);
        if (parsed is not null)
        {
            return parsed;
        }

        Fail(
            parameter,
            ErrorCodes.Search.InvalidFilterValue,
            new Dictionary<string, object?> { ["parameter"] = parameter, ["value"] = raw }
        );
        return null;
    }

    /// <summary>
    /// The parameter's query-string name is also the reported field: it is what the caller wrote in
    /// the URL, so it is what a message can point at. <c>parameter</c> and <c>value</c> ride along
    /// because the catalog sentences for these two codes interpolate them.
    /// </summary>
    private void Fail(
        string parameter,
        string code,
        IReadOnlyDictionary<string, object?> failureParams
    ) => _errors.Add(new FieldError(parameter, code, failureParams));
}
