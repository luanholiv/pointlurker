using Microsoft.Extensions.Options;

namespace PointLurker.Core.Application.Identity;

public sealed class CredentialExpiryOptionsValidator : IValidateOptions<CredentialExpiryOptions>
{
    public ValidateOptionsResult Validate(string? name, CredentialExpiryOptions options)
    {
        if (options.ExpiryWarningHours <= 0)
            return ValidateOptionsResult.Fail(
                $"CredentialExpiryOptions: ExpiryWarningHours must be greater than 0 (got {options.ExpiryWarningHours})"
            );

        return ValidateOptionsResult.Success;
    }
}
