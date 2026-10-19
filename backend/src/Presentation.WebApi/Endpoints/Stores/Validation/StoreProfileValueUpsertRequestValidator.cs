using FluentValidation;
using PointLurker.Core.Application.Stores;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

/// <summary>
/// One element of a profile-values upsert. A null <c>Value</c> is the documented way to remove a
/// stored value, so only its length is bounded — by the same constant the application layer
/// declares, which stays the single source of truth for the number.
/// </summary>
internal sealed class StoreProfileValueUpsertRequestValidator
    : AbstractValidator<StoreProfileValueUpsertRequest>
{
    public StoreProfileValueUpsertRequestValidator()
    {
        RuleFor(request => request.LabelCanonical)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.Required);

        RuleFor(request => request.Value)
            .MaximumLength(UpsertStoreProfileValues.MaxValueLength)
            .WithErrorCode(ErrorCodes.Validation.TooLong);
    }
}
