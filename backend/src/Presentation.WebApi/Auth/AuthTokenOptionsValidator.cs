using Microsoft.Extensions.Options;

namespace PointLurker.Presentation.WebApi.Auth;

public sealed class AuthTokenOptionsValidator : IValidateOptions<AuthTokenOptions>
{
    public ValidateOptionsResult Validate(string? name, AuthTokenOptions options)
    {
        if (options.AccessTokenExpirationMinutes < 1)
            return ValidateOptionsResult.Fail(
                $"AuthTokenOptions: AccessTokenExpirationMinutes must be at least 1 (got {options.AccessTokenExpirationMinutes})"
            );

        if (options.RefreshTokenExpirationHours < 1)
            return ValidateOptionsResult.Fail(
                $"AuthTokenOptions: RefreshTokenExpirationHours must be at least 1 (got {options.RefreshTokenExpirationHours})"
            );

        var access = TimeSpan.FromMinutes(options.AccessTokenExpirationMinutes);
        var refresh = TimeSpan.FromHours(options.RefreshTokenExpirationHours);
        if (access >= refresh)
            return ValidateOptionsResult.Fail(
                $"AuthTokenOptions: access-token lifetime ({access}) must be strictly shorter than the refresh-token lifetime ({refresh})"
            );

        return ValidateOptionsResult.Success;
    }
}
