namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The query string of <c>DELETE /api/stores/{id}/profile/aliases</c>. Idempotent like
/// <see cref="StoreProfileValueDeleteQuery"/>, so the label carries no validation rule.
/// </summary>
internal sealed record StoreProfileAliasDeleteQuery(string? RawLabel);
