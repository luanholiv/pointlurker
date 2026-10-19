using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Stores.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Stores.Validation;

internal sealed class UpsertStoreProfileAliasRequestValidator
    : AbstractValidator<UpsertStoreProfileAliasRequest>
{
    public UpsertStoreProfileAliasRequestValidator()
    {
        RuleFor(request => request.RawLabel)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.Required);

        RuleFor(request => request.LabelCanonical)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Validation.Required);
    }
}
