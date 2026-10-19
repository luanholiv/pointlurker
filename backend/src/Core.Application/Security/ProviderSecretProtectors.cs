using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Security;

/// <summary>
/// The Data Protection purpose each provider's credential is encrypted under. A provider absent
/// here cannot hold a credential: there would be no way to store its secret.
/// </summary>
public sealed class ProviderSecretProtectors(
    [FromKeyedServices(ProtectionPurposes.StreamElementsToken)] ISecretProtector streamElements,
    [FromKeyedServices(ProtectionPurposes.GauPointsSession)] ISecretProtector gauPoints
)
{
    /// <summary>The providers a credential can be stored for, in declaration order.</summary>
    public static IReadOnlyList<StoreProvider> Supported { get; } =
    [StoreProvider.StreamElements, StoreProvider.GauPoints];

    /// <summary>The protector for <paramref name="provider"/>, or null when it holds no credential.</summary>
    public ISecretProtector? For(StoreProvider provider) =>
        provider switch
        {
            StoreProvider.StreamElements => streamElements,
            StoreProvider.GauPoints => gauPoints,
            _ => null,
        };
}
