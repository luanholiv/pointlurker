using System.Buffers.Text;
using System.Text.Json;
using PointLurker.Core.Application.Stores;

namespace PointLurker.Infrastructure.ExternalServices.GauPointsApi;

/// <summary>
/// Reads the claims a <c>kp_session</c> JWT carries. Parsed, never verified: this application is
/// not the issuer and holds no key, so the payload is an untrusted hint and nothing here
/// authenticates anything. The value is a live credential and is never logged.
/// </summary>
internal static class GauPointsSession
{
    public static ProviderCredentialHints Describe(string session)
    {
        var parts = session.Split('.');
        if (parts.Length != 3 || !Base64Url.IsValid(parts[1]))
            return ProviderCredentialHints.None;

        try
        {
            using var payload = JsonDocument.Parse(Base64Url.DecodeFromChars(parts[1]));
            if (payload.RootElement.ValueKind != JsonValueKind.Object)
                return ProviderCredentialHints.None;

            DateTimeOffset? expiresAt =
                payload.RootElement.TryGetProperty("exp", out var exp)
                && exp.ValueKind == JsonValueKind.Number
                && exp.TryGetInt64(out var seconds)
                && seconds is > 0 and < 253402300800
                    ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                    : null;

            string? username =
                payload.RootElement.TryGetProperty("username", out var name)
                && name.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(name.GetString())
                    ? name.GetString()
                    : null;

            return new ProviderCredentialHints(expiresAt, username);
        }
        catch (JsonException)
        {
            return ProviderCredentialHints.None;
        }
    }
}
