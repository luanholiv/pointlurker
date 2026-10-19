using PointLurker.Core.Domain.Repositories;
using PointLurker.Core.Domain.Stores;

namespace PointLurker.Core.Application.Stores;

/// <summary>
/// The store input profile as the user experiences it: every raw label the store's items ask
/// for, classified as resolved or unresolved, plus the value slots that back them.
/// </summary>
/// <remarks>
/// Deliberately takes no <see cref="IStoreInputResolutionLogRepository" />. Everything here is a
/// live computation over the aggregate, which is what makes the profile observable without a
/// completed sync — the log is only ever written by the sync job.
/// </remarks>
public sealed class GetStoreInputProfile(
    IWatchedStoreRepository storeRepository,
    IInputLabelResolver inputLabelResolver
)
{
    public async Task<Result<StoreInputProfileResult, StoreError>> ExecuteAsync(
        Guid storeId,
        CancellationToken ct = default
    )
    {
        var store = await storeRepository.GetByIdAsync(storeId, ct);
        if (store is null)
        {
            return Result<StoreInputProfileResult, StoreError>.Failure(StoreError.NotFound());
        }

        var requests = CollectRequestedLabels(store);

        var requestersByCanonical = new Dictionary<string, List<InputRequesterDto>>(
            StringComparer.OrdinalIgnoreCase
        );
        var unresolved = new List<UnresolvedInputDto>();

        foreach (var request in requests)
        {
            var outcome = inputLabelResolver.Resolve(request.Label, store);

            // A resolution only counts as coverage when a value actually sits behind the
            // canonical. An alias can point at a canonical that was never given one.
            if (outcome is ResolutionResult.Resolved resolved && store.HasValue(resolved.Canonical))
            {
                if (!requestersByCanonical.TryGetValue(resolved.Canonical, out var requesters))
                {
                    requesters = [];
                    requestersByCanonical[resolved.Canonical] = requesters;
                }
                requesters.Add(
                    new InputRequesterDto(
                        request.Label,
                        resolved.Source,
                        resolved.Confidence,
                        request.ItemCount
                    )
                );
                continue;
            }

            unresolved.Add(
                new UnresolvedInputDto(
                    request.Label,
                    (outcome as ResolutionResult.Unresolved)?.BestCanonical,
                    (outcome as ResolutionResult.Unresolved)?.BestScore ?? 0,
                    (outcome as ResolutionResult.Resolved)?.Canonical,
                    request.ItemCount,
                    request.BlockedEntryCount
                )
            );
        }

        return Result<StoreInputProfileResult, StoreError>.Success(
            new StoreInputProfileResult(
                SortUnresolved(unresolved),
                BuildResolvedSlots(store, requestersByCanonical),
                store.InputValues.Select(v => v.LabelCanonical).ToArray()
            )
        );
    }

    private static IReadOnlyList<RequestedLabel> CollectRequestedLabels(WatchedStore store)
    {
        var requests = new Dictionary<string, RequestedLabel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in store.Items)
        {
            // One item declaring the same label twice is still one requester.
            var seenInItem = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in item.UserInputDefinitions)
            {
                if (string.IsNullOrWhiteSpace(definition))
                {
                    continue;
                }

                var label = WatchedStore.NormalizeLabel(definition);
                if (!seenInItem.Add(label))
                {
                    continue;
                }

                if (!requests.TryGetValue(label, out var request))
                {
                    request = new RequestedLabel(label);
                    requests[label] = request;
                }

                request.ItemCount++;
                if (item.WishlistEntry?.IsBlockedOnInput == true)
                {
                    request.BlockedEntryCount++;
                }
            }
        }
        return requests.Values.ToArray();
    }

    private static IReadOnlyList<UnresolvedInputDto> SortUnresolved(
        List<UnresolvedInputDto> unresolved
    ) =>
        unresolved
            .OrderByDescending(u => u.BlockedEntryCount)
            .ThenByDescending(u => u.ItemCount)
            .ThenBy(u => u.RawLabel, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<ResolvedInputDto> BuildResolvedSlots(
        WatchedStore store,
        Dictionary<string, List<InputRequesterDto>> requestersByCanonical
    ) =>
        store
            .InputValues.Select(value => new ResolvedInputDto(
                value.LabelCanonical,
                value.UpdatedAt,
                requestersByCanonical.TryGetValue(value.LabelCanonical, out var requesters)
                    ? requesters
                        .OrderBy(r => SourceRank(r.Source))
                        .ThenBy(r => r.RawLabel, StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                    : []
            ))
            .OrderByDescending(slot => slot.RequestedBy.Count > 0)
            .ThenBy(slot => slot.LabelCanonical, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static int SourceRank(ResolutionSource source) =>
        source switch
        {
            ResolutionSource.Exact => 0,
            ResolutionSource.Alias => 1,
            ResolutionSource.Fuzzy => 2,
            _ => 3,
        };

    private sealed class RequestedLabel(string label)
    {
        public string Label { get; } = label;
        public int ItemCount { get; set; }
        public int BlockedEntryCount { get; set; }
    }
}

public sealed record StoreInputProfileResult(
    IReadOnlyList<UnresolvedInputDto> Unresolved,
    IReadOnlyList<ResolvedInputDto> Resolved,
    IReadOnlyList<string> CanonicalLabels
);

/// <param name="AliasTarget">
/// Set only when a manual alias resolves this label to a canonical that holds no value — the one
/// way a label can be "mapped" and still uncovered. Null for every other unresolved label.
/// </param>
public sealed record UnresolvedInputDto(
    string RawLabel,
    string? BestCanonical,
    int BestScore,
    string? AliasTarget,
    int ItemCount,
    int BlockedEntryCount
);

public sealed record ResolvedInputDto(
    string LabelCanonical,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<InputRequesterDto> RequestedBy
);

public sealed record InputRequesterDto(
    string RawLabel,
    ResolutionSource Source,
    int Confidence,
    int ItemCount
);
