using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record StoreProfileValueUpsertRequest(string LabelCanonical, string? Value)
{
    /// <summary>
    /// Inbound direction: the contract converts itself into the application command, mirroring
    /// <c>RedeemRequest</c> in Infrastructure.ExternalServices.
    /// </summary>
    public static explicit operator StoreProfileValueUpsert(
        StoreProfileValueUpsertRequest request
    ) => new(request.LabelCanonical, request.Value);
}
