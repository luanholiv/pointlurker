using PointLurker.Core.Domain.Repositories;

namespace PointLurker.Core.Application.Stores;

public sealed class RemoveWishlistEntry(
    IWishlistEntryRepository wishlistEntryRepository,
    IUnitOfWork unitOfWork
)
{
    public async Task<Result<Unit, WishlistError>> ExecuteAsync(
        Guid entryId,
        CancellationToken ct = default
    )
    {
        var entry = await wishlistEntryRepository.GetForUpdateAsync(entryId, ct);
        if (entry is null)
        {
            return Result<Unit, WishlistError>.Failure(WishlistError.EntryNotFound());
        }

        wishlistEntryRepository.Remove(entry);
        await unitOfWork.SaveChangesAsync(ct);
        return Result<Unit, WishlistError>.Success(Unit.Value);
    }
}
