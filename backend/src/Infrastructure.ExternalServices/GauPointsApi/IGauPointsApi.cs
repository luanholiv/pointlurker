using Refit;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

internal interface IGauPointsApi
{
    [Get("/api/items")]
    Task<GauPointsItemsResponse> GetItemsAsync(CancellationToken cancellationToken = default);

    [Get("/api/me")]
    Task<GauPointsMeResponse> GetMeAsync(
        [Header("Cookie")] string cookie,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Answered as a response rather than thrown on an error status: a refusal carries the
    /// <c>{ok:false, error}</c> envelope on a 4xx, and the client has to read it to classify it.
    /// </summary>
    [Post("/api/redeem")]
    Task<IApiResponse<GauPointsRedeemResponse>> RedeemAsync(
        [Body] GauPointsRedeemRequest body,
        [Header("Cookie")] string cookie,
        [Header("Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken = default
    );
}
