using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

internal sealed record StoreProfileRequesterResponse(
    string RawLabel,
    string Source,
    int Confidence,
    int ItemCount
)
{
    public static explicit operator StoreProfileRequesterResponse(InputRequesterDto requester) =>
        new(
            requester.RawLabel,
            // Serialized as a name, not an ordinal: the frontend switches on it and no
            // JsonStringEnumConverter is registered app-wide.
            requester.Source.ToString(),
            requester.Confidence,
            requester.ItemCount
        );
}
