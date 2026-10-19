using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Users.Validation;

/// <summary>
/// A rule may name any declared code, not only a <c>validation.*</c> one — so a blank secret
/// reports that a credential is what is missing, instead of the generic per-field "required".
/// </summary>
internal sealed class SetCredentialRequestValidator : AbstractValidator<SetCredentialRequest>
{
    public SetCredentialRequestValidator()
    {
        RuleFor(request => request.Secret)
            .NotEmpty()
            .WithErrorCode(ErrorCodes.Credentials.SecretRequired);
    }
}
