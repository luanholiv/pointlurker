namespace PointLurker.Core.Domain.Stores;

public sealed class StoreInputResolutionLogEntry
{
    private StoreInputResolutionLogEntry() { }

    public StoreInputResolutionLogEntry(
        Guid id,
        Guid storeId,
        string rawLabel,
        string? canonicalLabel,
        int confidence,
        ResolutionSource source,
        DateTimeOffset observedAt
    )
    {
        if (string.IsNullOrWhiteSpace(rawLabel))
        {
            throw new ArgumentException("Raw label cannot be empty.", nameof(rawLabel));
        }
        if (confidence is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be 0..100.");
        }

        Id = id;
        StoreId = storeId;
        RawLabel = rawLabel;
        CanonicalLabel = canonicalLabel;
        Confidence = confidence;
        Source = source;
        ObservedAt = observedAt;
    }

    public Guid Id { get; private set; }
    public Guid StoreId { get; private set; }
    public string RawLabel { get; private set; } = string.Empty;
    public string? CanonicalLabel { get; private set; }
    public int Confidence { get; private set; }
    public ResolutionSource Source { get; private set; }
    public DateTimeOffset ObservedAt { get; private set; }
}

public enum ResolutionSource
{
    Alias,
    Fuzzy,
    Unresolved,

    /// <summary>
    /// The raw label equals a canonical label after whitespace normalization, ignoring case —
    /// the same rule <see cref="WatchedStore" /> uses to key stored values. Appended rather than
    /// ordered by precedence: the column is string-converted, so member order carries no meaning
    /// on the wire or in the database.
    /// </summary>
    Exact,
}
