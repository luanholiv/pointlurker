using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

public static class InputCoverage
{
    public static IReadOnlyList<string> ComputeMissingLabels(
        IReadOnlyList<string> definitions,
        WatchedStore store,
        IInputLabelResolver resolver
    )
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);

        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in definitions)
        {
            if (string.IsNullOrWhiteSpace(def) || !seen.Add(def))
            {
                continue;
            }

            var resolved = resolver.Resolve(def, store);
            if (resolved is not ResolutionResult.Resolved r || !store.HasValue(r.Canonical))
            {
                missing.Add(def);
            }
        }
        return missing;
    }

    public static IReadOnlyList<string> CollectStoreMissingLabels(
        WatchedStore store,
        IInputLabelResolver resolver
    )
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(resolver);

        var missing = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in store.Items)
        {
            foreach (var def in item.UserInputDefinitions)
            {
                if (string.IsNullOrWhiteSpace(def) || !seen.Add(def))
                {
                    continue;
                }
                var resolved = resolver.Resolve(def, store);
                if (resolved is not ResolutionResult.Resolved r || !store.HasValue(r.Canonical))
                {
                    missing.Add(def);
                }
            }
        }
        return missing;
    }
}
