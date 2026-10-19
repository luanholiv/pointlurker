using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>
/// The one store GauPoints is. It publishes no store metadata, so its identity is held here.
/// </summary>
internal static class GauPointsStore
{
    public static readonly StoreExternalId ExternalId = new("gaupoints");

    public const string DisplayName = "GauPoints";

    public const string AvatarUrl = "https://gaupoints.com.br/logo.png";

    public static ProviderStoreLookup Lookup { get; } =
        new(ExternalId, ExternalId.Value, DisplayName, AvatarUrl);
}
