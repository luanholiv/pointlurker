using Refit;

namespace PointLurker.Infrastructure.ExternalServices.StreamElementsApi;

internal interface IStreamElementsApi
{
    [Get("/kappa/v2/store/{channelId}/items?source=website")]
    Task<IReadOnlyList<StoreItemResponse>> GetStoreItemsAsync(
        string channelId,
        CancellationToken cancellationToken = default
    );

    [Get("/kappa/v2/channels/{username}")]
    Task<ChannelLookupResponse> GetChannelByUsernameAsync(
        string username,
        CancellationToken cancellationToken = default
    );

    [Post("/kappa/v2/store/{channel}/redemptions/{item}")]
    Task<RedemptionResponse> RedeemAsync(
        string channel,
        string item,
        [Body] RedeemRequest body,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default
    );

    [Get("/kappa/v2/store/{channel}/redemptions/{redemptionId}")]
    Task<GetRedemptionResponse> GetRedemptionAsync(
        string channel,
        string redemptionId,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default
    );

    [Get("/kappa/v2/users/current")]
    Task<CurrentUserResponse> GetCurrentUserAsync(
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default
    );

    [Get("/kappa/v2/points/{channel}/{username}")]
    Task<PointsResponse> GetPointsAsync(
        string channel,
        string username,
        [Header("Authorization")] string authorization,
        CancellationToken cancellationToken = default
    );
}
