namespace PointLurker.Presentation.WebApi.Validation;

/// <summary>
/// One entry of a <c>validation.failed</c> body's <c>params.errors</c> array: the field that broke,
/// the declared code naming why, and whatever that code's catalog sentence interpolates from.
/// </summary>
/// <param name="Field">
/// The name the client used — a camel-cased contract property for a body, a query-string parameter
/// name for the query surface. It is what a form anchors its message to.
/// </param>
/// <param name="Code">A declared <c>ErrorCodes.*</c> value, not necessarily a <c>validation.*</c> one.</param>
/// <param name="Params">
/// Extra interpolation values. <see cref="FieldErrorProblem"/> adds <c>field</c> itself, so a
/// producer with nothing else to say passes nothing.
/// </param>
internal sealed record FieldError(
    string Field,
    string Code,
    IReadOnlyDictionary<string, object?>? Params = null
);
