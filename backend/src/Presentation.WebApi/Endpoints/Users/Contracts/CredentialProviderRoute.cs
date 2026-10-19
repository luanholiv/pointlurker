namespace PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

/// <summary>
/// The <c>{provider}</c> segment of <c>/api/users/me/credentials/{provider}</c>, as a slug such as
/// <c>streamelements</c>. A string, parsed by the handler: an unknown provider is a
/// <c>credentials.provider_unsupported</c> answer the frontend can render, not a routing miss.
/// </summary>
internal sealed record CredentialProviderRoute(string? Provider);
