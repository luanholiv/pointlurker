namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

/// <param name="Provider">
/// The provider slug. Absent means StreamElements, the one provider there was before the field
/// existed.
/// </param>
/// <param name="ChannelId">The StreamElements channel id. Refused for a provider with one fixed store.</param>
/// <param name="DisplayName">The StreamElements channel's name. Refused likewise.</param>
internal sealed record CreateStoreRequest(string? Provider, string? ChannelId, string? DisplayName);
