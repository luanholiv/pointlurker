using NSubstitute;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;

namespace Core.Application.UnitTests.Stores;

/// <summary>A factory that hands out substitute clients by provider.</summary>
internal static class StoreProviderClients
{
    /// <summary>
    /// What a substitute reports unless a test says otherwise: the StreamElements shape, so a test
    /// written against one provider does not have to spell out capabilities it never reads.
    /// </summary>
    public static readonly StoreProviderCapabilities StreamElementsLike = new(
        SupportsMessages: true,
        SupportsMultipleStores: true,
        SupportsRedemptionPolling: true,
        ReturnsFulfillmentCode: false,
        SupportsUnlimitedStock: true,
        ReportsTotalStock: true,
        InputProfile: InputProfileKind.PerItemDeclared,
        ProvidesChannelMetadata: true,
        MinimumSyncInterval: TimeSpan.Zero
    );

    /// <summary>Hands out <paramref name="client"/> whichever provider is asked for.</summary>
    public static IStoreProviderClientFactory FactoryFor(IStoreProviderClient client)
    {
        client.Capabilities.Returns(StreamElementsLike);
        client.DescribeCredential(Arg.Any<string>()).Returns(ProviderCredentialHints.None);
        var factory = Substitute.For<IStoreProviderClientFactory>();
        factory.For(Arg.Any<StoreProvider>()).Returns(client);
        return factory;
    }
}
