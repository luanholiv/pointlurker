using Microsoft.Extensions.Options;

namespace PointLurker.Presentation.WebApi.Auth;

public sealed class IdentityPolicyOptionsValidator : IValidateOptions<IdentityPolicyOptions>
{
    public ValidateOptionsResult Validate(string? name, IdentityPolicyOptions options)
    {
        if (options.LockoutDurationMinutes < 1)
            return ValidateOptionsResult.Fail(
                $"IdentityPolicyOptions: LockoutDurationMinutes must be at least 1 (got {options.LockoutDurationMinutes})"
            );

        if (options.LockoutMaxFailedAttempts < 1)
            return ValidateOptionsResult.Fail(
                $"IdentityPolicyOptions: LockoutMaxFailedAttempts must be at least 1 (got {options.LockoutMaxFailedAttempts})"
            );

        if (options.PasswordMinimumLength < 1)
            return ValidateOptionsResult.Fail(
                $"IdentityPolicyOptions: PasswordMinimumLength must be at least 1 (got {options.PasswordMinimumLength})"
            );

        return ValidateOptionsResult.Success;
    }
}
