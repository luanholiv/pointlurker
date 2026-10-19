using FluentValidation;
using PointLurker.Core.Domain.Errors;
using PointLurker.Presentation.WebApi.Endpoints.Users.Contracts;

namespace PointLurker.Presentation.WebApi.Endpoints.Users.Validation;

/// <summary>
/// Shape only. Whether the locale is one this application supports is a domain allowlist, not
/// request shape: the handler answers that with <c>users.locale_unsupported</c> and the allowed
/// values, which is more than a per-field rule can say.
/// </summary>
internal sealed class SetLocaleRequestValidator : AbstractValidator<SetLocaleRequest>
{
    public SetLocaleRequestValidator()
    {
        RuleFor(request => request.Locale).NotEmpty().WithErrorCode(ErrorCodes.Validation.Required);
    }
}
