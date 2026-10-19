using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;
using Refit;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

// Internal: the only production construction site is the DI factory in
// GauPointsServiceCollectionExtensions. Consumers depend on the public IStoreProviderClient.
internal sealed class GauPointsApiClient(IGauPointsApi refit, IOptions<GauPointsSettings> settings)
    : IStoreProviderClient
{
    /// <summary>
    /// One fixed store with no messages, no way to read a redemption back and no store metadata to
    /// refresh. Stock is always counted and has no total, the inputs are a fixed form, and a digital
    /// redemption can answer with a code. Fetched at most every five minutes: a cron tuned for
    /// StreamElements would hammer a small self-hosted site.
    /// </summary>
    public static readonly StoreProviderCapabilities GauPointsCapabilities = new(
        SupportsMessages: false,
        SupportsMultipleStores: false,
        SupportsRedemptionPolling: false,
        ReturnsFulfillmentCode: true,
        SupportsUnlimitedStock: false,
        ReportsTotalStock: false,
        InputProfile: InputProfileKind.FixedSchema,
        ProvidesChannelMetadata: false,
        MinimumSyncInterval: TimeSpan.FromMinutes(5)
    );

    private readonly string _cookieName = settings.Value.SessionCookieName;

    public StoreProvider Provider => StoreProvider.GauPoints;

    public StoreProviderCapabilities Capabilities => GauPointsCapabilities;

    /// <summary>
    /// The query is ignored: there is one store. The catalogue is still fetched, so an unreachable
    /// site shows while the owner is looking rather than in a background sync hours later.
    /// </summary>
    public async Task<ProviderStoreLookup> LookupStoreAsync(
        string query,
        CancellationToken ct = default
    )
    {
        // The identity is a constant, but the catalogue read proves GauPoints is reachable while
        // the owner is still looking at the dialog, and yields a real item count for the preview.
        var response = await TranslateAsync(() => refit.GetItemsAsync(ct));
        return GauPointsStore.Lookup with { ItemCount = response.Items.Count };
    }

    public async Task<IReadOnlyList<ProviderStoreItem>> GetStoreItemsAsync(
        StoreExternalId store,
        CancellationToken ct = default
    )
    {
        var response = await TranslateAsync(() => refit.GetItemsAsync(ct));
        return response.Items.Select(item => (ProviderStoreItem)item).ToArray();
    }

    /// <summary>
    /// Classified on the status and <c>ok</c>, never on the Portuguese prose, which is user copy
    /// and will be reworded:
    /// <list type="bullet">
    /// <item>401 or 403: the session is dead — an error, reported as its status.</item>
    /// <item>408, 429 or 5xx: nothing was decided — an error, so the use case retries.</item>
    /// <item>Any other status with <c>ok:false</c>, or none at all: a refusal.</item>
    /// <item>2xx with <c>ok:true</c>, or with no <c>ok</c> at all: accepted.</item>
    /// </list>
    /// A 2xx that does not say <c>false</c> is taken as accepted because the points may already be
    /// spent: reading it as a failure would release the quota and redeem again.
    /// </summary>
    public async Task<ProviderRedemptionResult> RedeemAsync(
        ProviderRedeemCommand command,
        string credential,
        CancellationToken ct = default
    )
    {
        var request = (GauPointsRedeemRequest)command;
        var response = await TranslateAsync(() =>
            refit.RedeemAsync(
                request,
                Cookie(credential),
                command.RedemptionId.ToString("D", CultureInfo.InvariantCulture),
                ct
            )
        );

        // A response type does not throw: a request that never reached GauPoints comes back as an
        // error on the response instead, and is reported the same way the other calls report it.
        if (response.HasRequestError(out var requestError) || response.StatusCode is null)
        {
            throw Unreachable(
                (Exception?)requestError
                    ?? new HttpRequestException("GauPoints returned no response.")
            );
        }

        var status = (int)response.StatusCode.Value;
        var errorContent = response.HasResponseError(out var responseError)
            ? responseError.Content
            : null;

        if (status is 401 or 403 or 408 or 429 or >= 500)
        {
            throw new ProviderApiException(
                status,
                response.ReasonPhrase,
                $"GauPoints answered the redemption with HTTP {status}.",
                errorContent
            );
        }

        var body = response.IsSuccessStatusCode ? response.Content : ReadEnvelope(errorContent);

        if (response.IsSuccessStatusCode && body?.Ok is not false)
        {
            // Completed, not merely submitted: GauPoints offers no way to read a redemption back,
            // so this is the last thing that can be known about it. It records "accepted", never
            // "fulfilled".
            return new ProviderRedemptionResult.Accepted(
                RedemptionId: null,
                Completed: true,
                FulfillmentCode: string.IsNullOrWhiteSpace(body?.Code) ? null : body.Code
            );
        }

        return new ProviderRedemptionResult.Refused(
            $"HTTP {status}: {body?.Error ?? body?.Message ?? "no message"}"
        );
    }

    public Task<ProviderRedemptionStatus> GetRedemptionAsync(
        StoreExternalId store,
        string redemptionId,
        string credential,
        CancellationToken ct = default
    ) =>
        throw new NotSupportedException(
            "GauPoints exposes no redemption lookup; callers check SupportsRedemptionPolling."
        );

    public async Task<string> GetAccountNameAsync(string credential, CancellationToken ct = default)
    {
        var me = await GetSignedInAsync(credential, ct);

        return me.User?.Username
            ?? me.User?.Name
            ?? throw new ProviderApiException(
                (int)HttpStatusCode.OK,
                null,
                "GauPoints reported a signed-in session with no account name."
            );
    }

    /// <summary>One balance for the whole site: there is no per-store balance to pick.</summary>
    public async Task<long> GetPointsAsync(
        StoreExternalId store,
        string accountName,
        string credential,
        CancellationToken ct = default
    )
    {
        var me = await GetSignedInAsync(credential, ct);

        return me.Points
            ?? throw new ProviderApiException(
                (int)HttpStatusCode.OK,
                null,
                "GauPoints reported a signed-in session with no balance."
            );
    }

    public ProviderCredentialHints DescribeCredential(string credential) =>
        GauPointsSession.Describe(credential);

    /// <summary>
    /// <c>/api/me</c> answers 200 either way. Signed out is reported as the 401 it means, so every
    /// caller that already treats a 401 as a refused credential does so here too.
    /// </summary>
    private async Task<GauPointsMeResponse> GetSignedInAsync(
        string credential,
        CancellationToken ct
    )
    {
        var me = await TranslateAsync(() => refit.GetMeAsync(Cookie(credential), ct));

        return me.Logged
            ? me
            : throw new ProviderApiException(
                (int)HttpStatusCode.Unauthorized,
                nameof(HttpStatusCode.Unauthorized),
                "GauPoints reports the session as signed out."
            );
    }

    private string Cookie(string credential) => $"{_cookieName}={credential}";

    /// <summary>The <c>{ok, error}</c> envelope on an error status, or null when it is not one.</summary>
    private static GauPointsRedeemResponse? ReadEnvelope(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GauPointsRedeemResponse>(content);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// The one place Refit's exception types are translated, as in the StreamElements client.
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
