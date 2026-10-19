using Microsoft.Extensions.Options;

namespace PointLurker.Presentation.WebApi.Cors;

/// <summary>
/// Validates every configured origin on startup. <c>WithOrigins</c> accepts malformed values happily
/// and then silently never matches, so a bad origin would otherwise surface as "CORS just doesn't
/// work" at runtime. Accumulates every failure so one restart reports all of them.
/// </summary>
public sealed class CorsSettingsValidator : IValidateOptions<CorsSettings>
{
    public ValidateOptionsResult Validate(string? name, CorsSettings options)
    {
        var failures = new List<string>();

        for (var i = 0; i < options.AllowedOrigins.Length; i++)
        {
            var origin = options.AllowedOrigins[i];

            // Blank entries are inert (an unset Compose passthrough binds to an empty string), so
            // they are dropped rather than rejected — see CorsPolicyResolver.Normalize.
            if (string.IsNullOrWhiteSpace(origin))
                continue;

            var failure = Validate(origin.Trim());
            if (failure is not null)
                failures.Add($"CorsSettings: AllowedOrigins[{i}] {failure}");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static string? Validate(string origin)
    {
        if (origin == "*")
            return "must be a specific origin, not \"*\"; leave the list empty to disable CORS instead.";

        // Checked before parsing: Uri normalizes "https://host/" to an AbsolutePath of "/", so the
        // path check below cannot see it — yet the browser's Origin header never carries a trailing
        // slash, so such an entry would never match anything.
        if (origin.EndsWith('/'))
            return $"\"{origin}\" has a trailing slash; an origin is scheme + host + optional port only, and the browser's Origin header never sends one.";

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            return $"\"{origin}\" is not an absolute URI; use a full origin, e.g. https://monitor.example.com.";

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            return $"\"{origin}\" must use the http or https scheme (got \"{uri.Scheme}\").";

        if (!string.IsNullOrEmpty(uri.UserInfo))
            return $"\"{origin}\" must not contain user information.";

        if (
            uri.AbsolutePath != "/"
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
        )
            return $"\"{origin}\" must not contain a path, query or fragment; use scheme + host + optional port only.";

        return null;
    }
}
