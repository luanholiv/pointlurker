using PointLurker.Core.Domain.Stores.Enums;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// The name a provider goes by in a URL and in an error's params: its enum name in lower case. The
/// enum's numeric value is never accepted, because it is a persistence detail, not an address.
/// </summary>
public static class StoreProviderSlugs
{
    public static string ToSlug(this StoreProvider provider) =>
        provider.ToString().ToLowerInvariant();

    public static bool TryParse(string? slug, out StoreProvider provider)
    {
        foreach (var candidate in Enum.GetValues<StoreProvider>())
        {
            if (string.Equals(candidate.ToSlug(), slug, StringComparison.Ordinal))
            {
                provider = candidate;
                return true;
            }
        }

        provider = default;
        return false;
    }
}
