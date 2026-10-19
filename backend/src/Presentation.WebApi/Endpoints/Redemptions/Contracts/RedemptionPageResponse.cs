using PointLurker.Core.Application.Stores;

namespace PointLurker.Presentation.WebApi.Endpoints.Redemptions.Contracts;

/// <summary>
/// The shared page envelope, carrying redemption rows. The slice bounds travel as an offset and a
/// limit like every other list; the history's numbered pager derives them from the page number it
/// presents, which is a client concern.
/// </summary>
internal sealed record RedemptionPageResponse(
    IReadOnlyList<RedemptionItemResponse> Items,
    int TotalCount,
    int Offset,
    int Limit
)
{
    public static explicit operator RedemptionPageResponse(RedemptionListResult result) =>
        new(
            [.. result.Items.Select(item => (RedemptionItemResponse)item)],
            result.TotalCount,
            result.Offset,
            result.Limit
        );
}
