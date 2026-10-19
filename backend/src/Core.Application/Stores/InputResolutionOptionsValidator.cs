using Microsoft.Extensions.Options;

namespace PointLurker.Core.Application.Stores;

public sealed class InputResolutionOptionsValidator : IValidateOptions<InputResolutionOptions>
{
    public ValidateOptionsResult Validate(string? name, InputResolutionOptions options)
    {
        if (options.MatchThreshold is < 0 or > 100)
        {
            return ValidateOptionsResult.Fail(
                $"InputResolutionOptions: MatchThreshold must be in 0..100 (got {options.MatchThreshold})"
            );
        }

        return ValidateOptionsResult.Success;
    }
}
