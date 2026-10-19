using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class ListRedemptions(IRedemptionRepository redemptionRepo)
{
    public async Task<RedemptionListResult> ExecuteAsync(
        RedemptionFilter filter,
        int offset = 0,
        int limit = 20,
        CancellationToken ct = default
    )
    {
        var (rows, totalCount) = await redemptionRepo.ListAsync(filter, offset, limit, ct);

        return new RedemptionListResult(
            [.. rows.Select(row => (RedemptionDto)row)],
            totalCount,
            offset,
            limit
        );
    }
}
