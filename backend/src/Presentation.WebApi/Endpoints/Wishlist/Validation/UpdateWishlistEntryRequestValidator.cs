using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Core.Domain.Stores;
using PointLurker.Presentation.WebApi.Endpoints.Wishlist.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Wishlist.Validation;

internal sealed class UpdateWishlistEntryRequestValidator
    : AbstractValidator<UpdateWishlistEntryRequest>
{
    public UpdateWishlistEntryRequestValidator()
    {
        RuleFor(request => request.RedemptionMessage)
            .MaximumLength(WishlistEntry.RedemptionMessageMaxLength)
            .WithErrorCode(ErrorCodes.Validation.TooLong);

        RuleFor(request => request.DesiredRedemptions)
            .InclusiveBetween(1, WishlistEntry.DesiredRedemptionsMax)
            .WithErrorCode(ErrorCodes.Validation.OutOfRange)
            .When(request => request.DesiredRedemptions.HasValue);
    }
}
