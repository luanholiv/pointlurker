using Microsoft.Extensions.DependencyInjection;
using PointLurker.Core.Application.Security;
using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

public sealed class AddWishlistEntry(
    IStoreItemRepository storeItemRepository,
    IWishlistEntryRepository wishlistEntryRepository,
    IWatchedStoreRepository watchedStoreRepository,
    IInputLabelResolver inputLabelResolver,
    [FromKeyedServices(ProtectionPurposes.RedemptionMessage)] ISecretProtector messageProtector,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider
)
{
    public async Task<Result<AddWishlistEntrySuccess, WishlistError>> ExecuteAsync(
        Guid userId,
        Guid storeItemId,
        bool notifyOnAvailable,
        bool autoRedeem,
        int? desiredRedemptions,
        string? redemptionMessage,
        CancellationToken ct = default
    )
    {
        // With the store included: the created-entry response reports the store name, and it is
        // one join on a single-row lookup rather than a second round-trip.
        var storeItem = await storeItemRepository.GetByIdWithStoreAsync(storeItemId, ct);
        if (storeItem is null)
        {
            return Result<AddWishlistEntrySuccess, WishlistError>.Failure(
                WishlistError.ItemNotFound()
            );
        }

        var existing = await wishlistEntryRepository.GetByStoreItemAsync(userId, storeItemId, ct);
        if (existing is not null)
        {
            return Result<AddWishlistEntrySuccess, WishlistError>.Failure(
                WishlistError.AlreadyWishlisted()
            );
        }

        // A form posts the whole body, so an empty string is the absence of a message rather than
        // one. The entity still guards the rule; this answers it as a typed error instead of a 500.
        var message = string.IsNullOrEmpty(redemptionMessage) ? null : redemptionMessage;
        if (message is not null && !storeItem.AllowMessages)
        {
            return Result<AddWishlistEntrySuccess, WishlistError>.Failure(
                WishlistError.MessagesNotSupported()
            );
        }

        if (autoRedeem && storeItem.UserInputDefinitions.Count > 0)
        {
            var store = await watchedStoreRepository.GetProfileAsync(storeItem.WatchedStoreId, ct);
            if (store is null)
            {
                return Result<AddWishlistEntrySuccess, WishlistError>.Failure(
                    WishlistError.ItemNotFound()
                );
            }
            var missing = InputCoverage.ComputeMissingLabels(
                storeItem.UserInputDefinitions,
                store,
                inputLabelResolver
            );
            if (missing.Count > 0)
            {
                return Result<AddWishlistEntrySuccess, WishlistError>.Failure(
                    WishlistError.MissingInputCoverage(missing)
                );
            }
        }

        var storedMessage = message is null ? null : messageProtector.Protect(message);

        var now = timeProvider.GetUtcNow();
        var entry = new WishlistEntry(Guid.NewGuid(), userId, storeItemId, now);
        entry.SetNotifyOnAvailable(notifyOnAvailable, now);
        entry.SetDesiredRedemptions(desiredRedemptions, now);
        entry.SetRedemptionMessage(storedMessage, storeItem.AllowMessages, now);

        if (autoRedeem)
        {
            entry.EnableAutoRedeem(now);
        }

        wishlistEntryRepository.Add(entry);
        await unitOfWork.SaveChangesAsync(ct);

        return Result<AddWishlistEntrySuccess, WishlistError>.Success(
            new AddWishlistEntrySuccess(entry, storeItem)
        );
    }
}

public sealed record AddWishlistEntrySuccess(WishlistEntry Entry, StoreItem Item);
