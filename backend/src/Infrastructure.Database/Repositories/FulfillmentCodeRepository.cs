using Microsoft.EntityFrameworkCore;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Infrastructure.Database.Repositories;

public sealed class FulfillmentCodeRepository(AppDbContext dbContext) : IFulfillmentCodeRepository
{
    public void Add(FulfillmentCode code) => dbContext.FulfillmentCodes.Add(code);

    public void Update(FulfillmentCode code) =>
        dbContext.EnsureTrackedForUpdate(code, nameof(GetDueForUpdateAsync));

    public async Task<IReadOnlyList<PendingFulfillmentDelivery>> GetDueForUpdateAsync(
        DateTimeOffset now,
        CancellationToken ct = default
    )
    {
        // The code is tracked through the projection; the redemption's columns come along as plain
        // values, so nothing about the redemption is loaded to be written.
        return await dbContext
            .FulfillmentCodes.AsTracking()
            .Where(code =>
                code.SentAt == null && (code.NextRetryAt == null || code.NextRetryAt <= now)
            )
            .OrderBy(code => code.CreatedAt)
            .Join(
                dbContext.Redemptions,
                code => code.RedemptionId,
                redemption => redemption.Id,
                (code, redemption) =>
                    new PendingFulfillmentDelivery(
                        code,
                        redemption.StoreItemId,
                        redemption.StoreItemName,
                        redemption.StoreName
                    )
            )
            .ToListAsync(ct);
    }
}
