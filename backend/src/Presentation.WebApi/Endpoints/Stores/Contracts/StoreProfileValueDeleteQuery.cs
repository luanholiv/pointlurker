namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <summary>
/// The query string of <c>DELETE /api/stores/{id}/profile/values</c>. The label travels in the query
/// string rather than the route because it is free text, and it carries no validation rule: the
/// delete is idempotent, so an absent label removes nothing and still answers <c>204</c>.
/// </summary>
internal sealed record StoreProfileValueDeleteQuery(string? Label);
