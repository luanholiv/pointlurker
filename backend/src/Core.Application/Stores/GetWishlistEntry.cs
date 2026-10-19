using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

public sealed class GetWishlistEntry(
    IWishlistEntryRepository wishlistEntryRepository,
    IStoreItemRepository storeItemRepository,
    IWatchedStoreRepository storeRepository,
    IRedemptionRepository redemptionRepository,
    IInputLabelResolver inputLabelResolver,
    [FromKeyedServices(ProtectionPurposes.RedemptionMessage)] ISecretProtector messageProtector
)
{
    public async Task<Result<GetWishlistEntryResult, WishlistError>> ExecuteAsync(
        Guid entryId,
        CancellationToken ct = default
    )
    {
        var entry = await wishlistEntryRepository.GetByIdAsync(entryId, ct);
        if (entry is null)
        {
            return Result<GetWishlistEntryResult, WishlistError>.Failure(
                WishlistError.EntryNotFound()
            );
        }

        var storeItem = await storeItemRepository.GetByIdAsync(entry.StoreItemId, ct);
        if (storeItem is null)
        {
            return Result<GetWishlistEntryResult, WishlistError>.Failure(
                WishlistError.ItemNotFound()
            );
        }

        var store = await storeRepository.GetProfileAsync(storeItem.WatchedStoreId, ct);

        IReadOnlyList<string> missing = Array.Empty<string>();
        if (store is not null && storeItem.UserInputDefinitions.Count > 0)
        {
            missing = InputCoverage.ComputeMissingLabels(
                storeItem.UserInputDefinitions,
                store,
                inputLabelResolver
            );
        }

        var isBlocked = entry.AutoRedeem && missing.Count > 0;

        var quotaUsage = await redemptionRepository.CountHoldingQuotaAsync([entry.Id], ct);
        var quotaUsed = quotaUsage.GetValueOrDefault(entry.Id).QuotaUsed;

        var redemptionMessage = entry.RedemptionMessage is { Length: > 0 } ciphertext
            ? messageProtector.Unprotect(ciphertext)
            : entry.RedemptionMessage;

        return Result<GetWishlistEntryResult, WishlistError>.Success(
            new GetWishlistEntryResult(
                entry.Id,
                entry.StoreItemId,
                storeItem.Name,
                storeItem.Cost,
                storeItem.Availability.IsAvailable,
                store?.DisplayName ?? string.Empty,
                store?.Id ?? Guid.Empty,
                entry.NotifyOnAvailable,
                entry.AutoRedeem,
                entry.DesiredRedemptions,
                quotaUsed,
                storeItem.AllowMessages,
                redemptionMessage,
                isBlocked,
                missing,
                entry.CreatedAt,
                entry.UpdatedAt
            )
        );
    }
}

public sealed record GetWishlistEntryResult(
    Guid Id,
    Guid StoreItemId,
    string ItemName,
    long ItemCost,
    bool IsAvailable,
    string StoreName,
    Guid StoreId,
    bool NotifyOnAvailable,
    bool AutoRedeem,
    int? DesiredRedemptions,
    int QuotaUsed,
    bool AllowMessages,
    string? RedemptionMessage,
    bool IsBlocked,
    IReadOnlyList<string> MissingLabels,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt
);
