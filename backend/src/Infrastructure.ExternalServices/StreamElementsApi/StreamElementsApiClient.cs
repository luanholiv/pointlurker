using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using Refit;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

// Internal: the only production construction site is the DI factory in
// StreamElementsServiceCollectionExtensions. Consumers depend on the public IStoreProviderClient.
internal sealed class StreamElementsApiClient(IStreamElementsApi refit) : IStoreProviderClient
{
    /// <summary>
    /// Any number of channels, each item declaring its own inputs, messages allowed, redemptions
    /// read back by id, and stock that can be unlimited and always carries a total. Synced on every
    /// tick: the fetch cron is tuned for this provider.
    /// </summary>
    public static readonly StoreProviderCapabilities StreamElementsCapabilities = new(
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

    public StoreProvider Provider => StoreProvider.StreamElements;

    public StoreProviderCapabilities Capabilities => StreamElementsCapabilities;

    public async Task<ProviderStoreLookup> LookupStoreAsync(
        string query,
        CancellationToken ct = default
    ) =>
        (ProviderStoreLookup)await TranslateAsync(() => refit.GetChannelByUsernameAsync(query, ct));

    public async Task<IReadOnlyList<ProviderStoreItem>> GetStoreItemsAsync(
        StoreExternalId store,
        CancellationToken ct = default
    )
    {
        var response = await TranslateAsync(() => refit.GetStoreItemsAsync(store.Value, ct));

        var mapped = new ProviderStoreItem[response.Count];
        for (var i = 0; i < response.Count; i++)
        {
            mapped[i] = (ProviderStoreItem)response[i];
        }
        return mapped;
    }

    public async Task<ProviderRedemptionResult> RedeemAsync(
        ProviderRedeemCommand command,
        string credential,
        CancellationToken ct = default
    )
    {
        var request = (RedeemRequest)command;

        try
        {
            return (ProviderRedemptionResult)
                await TranslateAsync(() =>
                    refit.RedeemAsync(
                        command.Store.Value,
                        command.Item.Value,
                        request,
                        $"Bearer {credential}",
                        ct
                    )
                );
        }
        catch (ProviderApiException ex) when (IsInsufficientPoints(ex))
        {
            return new ProviderRedemptionResult.InsufficientPoints($"HTTP 400: {ex.Content}");
        }
    }

    /// <summary>
    /// StreamElements answers too few points with a 400 whose body reads
    /// <c>{"message":"Sorry you don't have enough points"}</c>. Its prose is the only signal, so it
    /// is read here, where this provider's wire is known, and nowhere above the port.
    /// </summary>
    private static bool IsInsufficientPoints(ProviderApiException ex) =>
        ex.StatusCode == 400
        && ex.Content is not null
        && ex.Content.Contains("enough points", StringComparison.OrdinalIgnoreCase);

    public async Task<ProviderRedemptionStatus> GetRedemptionAsync(
        StoreExternalId store,
        string redemptionId,
        string credential,
        CancellationToken ct = default
    ) =>
        (ProviderRedemptionStatus)
            await TranslateAsync(() =>
                refit.GetRedemptionAsync(store.Value, redemptionId, $"Bearer {credential}", ct)
            );

    public async Task<string> GetAccountNameAsync(string credential, CancellationToken ct = default)
    {
        var response = await TranslateAsync(() =>
            refit.GetCurrentUserAsync($"Bearer {credential}", ct)
        );

        return response.Username;
    }

    /// <summary>A StreamElements JWT is read as opaque: its lifetime is not what ends it.</summary>
    public ProviderCredentialHints DescribeCredential(string credential) =>
        ProviderCredentialHints.None;

    public async Task<long> GetPointsAsync(
        StoreExternalId store,
        string accountName,
        string credential,
        CancellationToken ct = default
    )
    {
        var response = await TranslateAsync(() =>
            refit.GetPointsAsync(store.Value, accountName, $"Bearer {credential}", ct)
        );

        return response.Points;
    }

    /// <summary>
    /// The one place Refit's exception types are translated, so no caller outside this assembly has
    /// to know the HTTP client library. Both failure shapes have to be handled: a response that
    /// arrived carrying an error status, and a request that never reached StreamElements at all.
    /// Cancellation is deliberately not translated — it is the caller's own signal coming back.
    /// </summary>
    private static async Task<T> TranslateAsync<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ApiException ex)
        {
            throw new ProviderApiException(
                (int)ex.StatusCode,
                ex.ReasonPhrase,
                ex.Message,
                ex.Content,
                ex
            );
        }
        catch (ApiRequestException ex)
        {
            throw Unreachable(ex);
        }
        catch (HttpRequestException ex)
        {
            throw Unreachable(ex);
        }
    }

    private static ProviderApiException Unreachable(Exception ex) =>
        new(ProviderApiException.NoResponse, null, ex.Message, null, ex);
}
