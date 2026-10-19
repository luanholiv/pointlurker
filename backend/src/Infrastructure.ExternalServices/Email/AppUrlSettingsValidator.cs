using Microsoft.Extensions.Options;

namespace PointLurker.Infrastructure.ExternalServices.Email;

/// <summary>
/// Validates <see cref="AppUrlSettings"/>. An unset value is valid — it means "render no links".
/// A set value must be an absolute http(s) origin with nothing after the authority, because the
/// template appends paths to it directly.
/// All failures are returned together so each is logged on startup.
/// </summary>
public sealed class AppUrlSettingsValidator : IValidateOptions<AppUrlSettings>
{
    public ValidateOptionsResult Validate(string? name, AppUrlSettings options)
    {
        var value = options.PublicBaseUrl;

        // Unset and blank mean the same thing: a deployment that publishes no console URL.
        // Normalising to null here keeps the template's only check a null check.
        if (string.IsNullOrWhiteSpace(value))
        {
            options.PublicBaseUrl = null;
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            failures.Add("AppUrlSettings: PublicBaseUrl must be an absolute URL");
            return ValidateOptionsResult.Fail(failures);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("AppUrlSettings: PublicBaseUrl must use the http or https scheme");
        }

        // AbsolutePath is "/" for a bare origin, so anything longer is a path segment. A lone
        // trailing slash is caught by the string check rather than the parse, because Uri
        // normalises "https://host/" and "https://host" to the same AbsolutePath.
        if (uri.AbsolutePath != "/" || value.EndsWith('/'))
        {
            failures.Add("AppUrlSettings: PublicBaseUrl must not end with a slash or carry a path");
        }

        if (!string.IsNullOrEmpty(uri.Query))
        {
            failures.Add("AppUrlSettings: PublicBaseUrl must not carry a query string");
        }

        if (!string.IsNullOrEmpty(uri.Fragment))
        {
            failures.Add("AppUrlSettings: PublicBaseUrl must not carry a fragment");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
