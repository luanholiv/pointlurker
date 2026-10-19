using FuzzySharp;
using Microsoft.Extensions.Options;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

public sealed class FuzzyInputLabelResolver(IOptions<InputResolutionOptions> options)
    : IInputLabelResolver
{
    public ResolutionResult Resolve(string rawLabel, WatchedStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        var normalized = Normalize(rawLabel);
        if (string.IsNullOrEmpty(normalized))
        {
            return new ResolutionResult.Unresolved(0);
        }

        foreach (var alias in store.InputAliases)
        {
            if (
                string.Equals(
                    Normalize(alias.RawLabel),
                    normalized,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return new ResolutionResult.Resolved(
                    alias.LabelCanonical,
                    100,
                    ResolutionSource.Alias
                );
            }
        }

        // Exact before fuzzy. TokenSetRatio scores identical token sets at 100 regardless of
        // order, and the loop below keeps its best with a strict `>`, so without this step
        // "Twitch Username" could resolve to an earlier-inserted "Username Twitch".
        foreach (var value in store.InputValues)
        {
            if (
                string.Equals(
                    Normalize(value.LabelCanonical),
                    normalized,
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                return new ResolutionResult.Resolved(
                    value.LabelCanonical,
                    100,
                    ResolutionSource.Exact
                );
            }
        }

        var threshold = options.Value.MatchThreshold;
        var bestScore = 0;
        string? bestCanonical = null;

        foreach (var value in store.InputValues)
        {
            var canonicalNormalized = Normalize(value.LabelCanonical);
            var score = Fuzz.TokenSetRatio(
                normalized.ToLowerInvariant(),
                canonicalNormalized.ToLowerInvariant()
            );
            if (score > bestScore)
            {
                bestScore = score;
                bestCanonical = value.LabelCanonical;
            }
        }

        if (bestCanonical is not null && bestScore >= threshold)
        {
            return new ResolutionResult.Resolved(bestCanonical, bestScore, ResolutionSource.Fuzzy);
        }

        // Carry the best (sub-threshold) candidate so callers can surface it as a
        // mapping suggestion. Null when the store has no canonical labels.
        return new ResolutionResult.Unresolved(bestScore, bestCanonical);
    }

    private static string Normalize(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }
        var trimmed = label.Trim();
        return System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ");
    }
}
