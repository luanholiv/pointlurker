using PointLurker.Core.Application.Errors;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

public readonly record struct WishlistError(
    string Code,
    ErrorKind Kind,
    string Title,
    IReadOnlyDictionary<string, object?>? Params = null
) : IApplicationError
{
    public static WishlistError EntryNotFound() =>
        new(ErrorCodes.Wishlist.EntryNotFound, ErrorKind.NotFound, "Wishlist entry not found.");

    public static WishlistError ItemNotFound() =>
        new(ErrorCodes.Wishlist.ItemNotFound, ErrorKind.NotFound, "Store item not found.");

    public static WishlistError AlreadyWishlisted() =>
        new(ErrorCodes.Wishlist.AlreadyExists, ErrorKind.Conflict, "Item is already wishlisted.");

    public static WishlistError MessagesNotSupported() =>
        new(
            ErrorCodes.Wishlist.MessagesNotSupported,
            ErrorKind.Unprocessable,
            "The item does not accept a redemption message."
        );

    public static WishlistError MissingInputCoverage(IReadOnlyList<string> missingLabels) =>
        new(
            ErrorCodes.Wishlist.MissingInputCoverage,
            ErrorKind.Unprocessable,
            "Auto-redeem requires the store profile to cover all item input labels.",
            new Dictionary<string, object?> { ["missingLabels"] = missingLabels }
        );
}
