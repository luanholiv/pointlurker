using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Domain.Stores;

public sealed class WatchedStore
{
    private readonly List<StoreItem> _items = [];
    private readonly List<StoreInputValue> _inputValues = [];
    private readonly List<StoreInputAlias> _inputAliases = [];

    private WatchedStore() { }

    public WatchedStore(
        Guid id,
        StoreProvider provider,
        StoreExternalId externalId,
        string displayName,
        Guid userId,
        DateTimeOffset createdAt,
        string? avatarUrl = null
    )
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException("Display name cannot be empty.", nameof(displayName));
        }

        Id = id;
        Provider = provider;
        ExternalId = externalId;
        DisplayName = displayName;
        UserId = userId;
        IsActive = true;
        CreatedAt = createdAt;
        AvatarUrl = NormalizeUrl(avatarUrl);
    }

    public Guid Id { get; private set; }
    public StoreProvider Provider { get; private set; }
    public StoreExternalId ExternalId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? AvatarUrl { get; private set; }
    public Guid UserId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastSyncedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<StoreItem> Items => _items;
    public IReadOnlyList<StoreInputValue> InputValues => _inputValues;
    public IReadOnlyList<StoreInputAlias> InputAliases => _inputAliases;

    public void AddItem(StoreItem item)
    {
        _items.Add(item);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void RecordSync(DateTimeOffset syncedAt)
    {
        LastSyncedAt = syncedAt;
    }

    public void UpdateChannelInfo(string displayName, string? avatarUrl)
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            DisplayName = displayName.Trim();
        }

        AvatarUrl = NormalizeUrl(avatarUrl);
    }

    private static string? NormalizeUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.Trim();

    public void UpsertValue(string canonicalLabel, string ciphertext, DateTimeOffset updatedAt)
    {
        var canonical = NormalizeLabel(canonicalLabel);
        if (string.IsNullOrEmpty(canonical))
        {
            throw new ArgumentException("Canonical label cannot be empty.", nameof(canonicalLabel));
        }
        ArgumentException.ThrowIfNullOrEmpty(ciphertext);

        var index = _inputValues.FindIndex(v =>
            string.Equals(v.LabelCanonical, canonical, StringComparison.OrdinalIgnoreCase)
        );
        var entry = new StoreInputValue(canonical, ciphertext, updatedAt);
        if (index >= 0)
        {
            _inputValues[index] = entry;
        }
        else
        {
            _inputValues.Add(entry);
        }
    }

    public bool RemoveValue(string canonicalLabel)
    {
        var canonical = NormalizeLabel(canonicalLabel);
        var removed = _inputValues.RemoveAll(v =>
            string.Equals(v.LabelCanonical, canonical, StringComparison.OrdinalIgnoreCase)
        );
        return removed > 0;
    }

    public void UpsertAlias(string rawLabel, string canonicalLabel)
    {
        var raw = NormalizeLabel(rawLabel);
        var canonical = NormalizeLabel(canonicalLabel);
        if (string.IsNullOrEmpty(raw))
        {
            throw new ArgumentException("Raw label cannot be empty.", nameof(rawLabel));
        }
        if (string.IsNullOrEmpty(canonical))
        {
            throw new ArgumentException("Canonical label cannot be empty.", nameof(canonicalLabel));
        }

        var index = _inputAliases.FindIndex(a =>
            string.Equals(a.RawLabel, raw, StringComparison.OrdinalIgnoreCase)
        );
        var entry = new StoreInputAlias(raw, canonical);
        if (index >= 0)
        {
            _inputAliases[index] = entry;
        }
        else
        {
            _inputAliases.Add(entry);
        }
    }

    public bool RemoveAlias(string rawLabel)
    {
        var raw = NormalizeLabel(rawLabel);
        var removed = _inputAliases.RemoveAll(a =>
            string.Equals(a.RawLabel, raw, StringComparison.OrdinalIgnoreCase)
        );
        return removed > 0;
    }

    public string? GetCiphertext(string canonicalLabel)
    {
        var canonical = NormalizeLabel(canonicalLabel);
        var match = _inputValues.FirstOrDefault(v =>
            string.Equals(v.LabelCanonical, canonical, StringComparison.OrdinalIgnoreCase)
        );
        return match?.EncryptedValue;
    }

    public bool HasValue(string canonicalLabel)
    {
        var canonical = NormalizeLabel(canonicalLabel);
        return _inputValues.Any(v =>
            string.Equals(v.LabelCanonical, canonical, StringComparison.OrdinalIgnoreCase)
        );
    }

    /// <summary>
    /// The canonical keying rule for input labels: trim, then collapse internal whitespace.
    /// Case is preserved for display; every comparison against it is OrdinalIgnoreCase.
    /// Public so read models can group raw labels exactly the way the aggregate keys values,
    /// rather than growing another copy of the rule.
    /// </summary>
    public static string NormalizeLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return string.Empty;
        }
        var trimmed = label.Trim();
        return System.Text.RegularExpressions.Regex.Replace(trimmed, @"\s+", " ");
    }
}
