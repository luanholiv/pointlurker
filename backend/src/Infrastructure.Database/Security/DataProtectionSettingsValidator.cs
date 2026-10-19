using Microsoft.Extensions.Options;

namespace PointLurker.Infrastructure.Database.Security;

public sealed class DataProtectionSettingsValidator : IValidateOptions<DataProtectionSettings>
{
    public ValidateOptionsResult Validate(string? name, DataProtectionSettings options)
    {
        // Blank is the [Required] annotation's failure to report — it runs first, so staying quiet
        // here keeps the failure from being listed twice.
        if (string.IsNullOrWhiteSpace(options.KeyRingPath))
            return ValidateOptionsResult.Success;

        // Catches what the annotation cannot: a path the platform refuses outright (embedded nulls,
        // and on Windows the reserved characters). Side-effect free — the directory is created when
        // the key ring is first used, not here.
        try
        {
            Path.GetFullPath(options.KeyRingPath);
        }
        catch (Exception exception)
            when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ValidateOptionsResult.Fail(
                $"DataProtectionSettings: KeyRingPath is not a valid path ({exception.Message})"
            );
        }

        return ValidateOptionsResult.Success;
    }
}
