using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// One upstream points store, reached through whatever API it exposes. Every call site picks an
/// implementation by <see cref="StoreProvider"/> through <see cref="IStoreProviderClientFactory"/>,
/// so nothing above this port knows which service it is talking to.
/// <para>
/// Every failure is reported as <see cref="ProviderApiException"/>; cancellation is the caller's own
/// signal and passes through untouched.
/// </para>
/// </summary>
public interface IStoreProviderClient
{
    StoreProvider Provider { get; }

    StoreProviderCapabilities Capabilities { get; }

    /// <summary>Finds a store by what a user would type for it — for StreamElements, a channel handle or id.</summary>
    Task<ProviderStoreLookup> LookupStoreAsync(string query, CancellationToken ct = default);

    Task<IReadOnlyList<ProviderStoreItem>> GetStoreItemsAsync(
        StoreExternalId store,
        CancellationToken ct = default
    );

    Task<ProviderRedemptionResult> RedeemAsync(
        ProviderRedeemCommand command,
        string credential,
        CancellationToken ct = default
    );

    Task<ProviderRedemptionStatus> GetRedemptionAsync(
        StoreExternalId store,
        string redemptionId,
        string credential,
        CancellationToken ct = default
    );

    /// <summary>The name the credential's owner is known by upstream.</summary>
    Task<string> GetAccountNameAsync(string credential, CancellationToken ct = default);

    /// <summary>
    /// Reads what <paramref name="credential"/> reveals about itself, with no network call.
    /// <see cref="ProviderCredentialHints.None"/> for an opaque secret, or one that does not parse.
    /// </summary>
    ProviderCredentialHints DescribeCredential(string credential);

    /// <summary>
    /// The owner's balance in one store. Per store, because StreamElements keeps a separate balance
    /// for every channel; <paramref name="accountName"/> is what <see cref="GetAccountNameAsync"/>
    /// last returned.
    /// </summary>
    Task<long> GetPointsAsync(
        StoreExternalId store,
        string accountName,
        string credential,
        CancellationToken ct = default
    );
}
