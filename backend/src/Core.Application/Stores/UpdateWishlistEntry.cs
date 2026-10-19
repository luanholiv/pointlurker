using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

public sealed class UpdateWishlistEntry(
    IStoreItemRepository storeItemRepository,
    IWishlistEntryRepository wishlistEntryRepository,
    IWatchedStoreRepository watchedStoreRepository,
    IInputLabelResolver inputLabelResolver,
    [FromKeyedServices(ProtectionPurposes.RedemptionMessage)] ISecretProtector messageProtector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider
)
{
    public async Task<Result<Unit, WishlistError>> ExecuteAsync(
        Guid entryId,
        bool notifyOnAvailable,
        bool autoRedeem,
        int? desiredRedemptions,
        string? redemptionMessage,
        CancellationToken ct = default
    )
    {
        var entry = await wishlistEntryRepository.GetForUpdateAsync(entryId, ct);
        if (entry is null)
        {
            return Result<Unit, WishlistError>.Failure(WishlistError.EntryNotFound());
        }

        var storeItem = await storeItemRepository.GetByIdAsync(entry.StoreItemId, ct);
        if (storeItem is null)
        {
            return Result<Unit, WishlistError>.Failure(WishlistError.EntryNotFound());
        }

        // A form posts the whole body, so an empty string is the absence of a message rather than
        // one. The entity still guards the rule; this answers it as a typed error instead of a 500.
        var message = string.IsNullOrEmpty(redemptionMessage) ? null : redemptionMessage;
        if (message is not null && !storeItem.AllowMessages)
        {
            return Result<Unit, WishlistError>.Failure(WishlistError.MessagesNotSupported());
        }

        if (autoRedeem && storeItem.UserInputDefinitions.Count > 0)
        {
            var store = await watchedStoreRepository.GetProfileAsync(storeItem.WatchedStoreId, ct);
            if (store is null)
            {
                return Result<Unit, WishlistError>.Failure(WishlistError.EntryNotFound());
            }
            var missing = InputCoverage.ComputeMissingLabels(
                storeItem.UserInputDefinitions,
                store,
                inputLabelResolver
            );
            if (missing.Count > 0)
            {
                return Result<Unit, WishlistError>.Failure(
                    WishlistError.MissingInputCoverage(missing)
                );
            }
        }

        var storedMessage = message is null ? null : messageProtector.Protect(message);

        var now = timeProvider.GetUtcNow();
        entry.SetNotifyOnAvailable(notifyOnAvailable, now);

        // Raising the count re-arms an entry that already met its quota; that is the only way a
        // spent entry becomes live again, so it is applied whatever auto-redeem is set to.
        entry.SetDesiredRedemptions(desiredRedemptions, now);

        entry.SetRedemptionMessage(storedMessage, storeItem.AllowMessages, now);

        if (autoRedeem)
        {
            entry.EnableAutoRedeem(now);
        }
        else
        {
            entry.DisableAutoRedeem(now);
        }

        wishlistEntryRepository.Update(entry);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<Unit, WishlistError>.Success(Unit.Value);
    }
}
