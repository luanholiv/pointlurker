using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Validation;

internal sealed class CreateWishlistEntryRequestValidator
    : AbstractValidator<CreateWishlistEntryRequest>
{
    public CreateWishlistEntryRequestValidator()
    {
        RuleFor(request => request.StoreItemId)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.Required);

        RuleFor(request => request.RedemptionMessage)
            .MaximumLength(WishlistEntry.RedemptionMessageMaxLength)
            .WithErrorCode(ErrorCodes.Validation.TooLong);

        // Null is unlimited and passes; the bounds apply to a stated count. The upper bound is a
        // safety rail — this many redemptions will be paid for without asking again.
        RuleFor(request => request.DesiredRedemptions)
            .InclusiveBetween(1, WishlistEntry.DesiredRedemptionsMax)
            .WithErrorCode(ErrorCodes.Validation.OutOfRange)
            .When(request => request.DesiredRedemptions.HasValue);
    }
}
