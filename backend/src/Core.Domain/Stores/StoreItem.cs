using System.Linq.Expressions;
using PointLurker.Core.Domain.Common;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace PointLurker.Core.Domain.Stores;

public sealed class StoreItem
{
    private readonly List<IDomainEvent> _domainEvents = [];

    private StoreItem() { }

    public StoreItem(Guid id, Guid watchedStoreId, StoreItemApiData data)
    {
        Id = id;
        WatchedStoreId = watchedStoreId;
        ApplyData(data, ItemAvailability.From(false, new ItemQuantity(0, 0)));
        LastChangedAt = TruncateToStoredPrecision(data.LastFetchedAt);

        if (Availability.IsAvailable)
        {
            _domainEvents.Add(
                new StoreItemBecameAvailable(Id, WatchedStoreId, Name, data.LastFetchedAt)
            );
        }
    }

    /// <summary>
    /// The availability invariant in a form a query provider can translate, so filtering by
    /// availability in SQL and evaluating <see cref="Availability" /> in memory cannot drift apart.
    /// Query code composes this rather than restating the rule; a truth-table test pins it against
    /// <see cref="ItemAvailability.From" />. <c>Current == -1</c> means unlimited and is available.
    /// </summary>
    public static Expression<Func<StoreItem, bool>> IsAvailableExpression =>
        item => item.Enabled && (item.Quantity.Current == -1 || item.Quantity.Current > 0);

    public Guid Id { get; private set; }
    public Guid WatchedStoreId { get; private set; }
    public StoreProvider Provider { get; private set; }
    public ProviderItemId ProviderItemId { get; private set; }
    public StoreExternalId StoreExternalId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public long Cost { get; private set; }
    public string CategoryName { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public bool Featured { get; private set; }
    public bool Public { get; private set; }
    public bool SubscriberOnly { get; private set; }
    public bool AllowMessages { get; private set; }
    public IReadOnlyList<string> UserInputDefinitions { get; private set; } = Array.Empty<string>();
    public IReadOnlyList<string> Sources { get; private set; } = Array.Empty<string>();
    public ItemQuantity Quantity { get; private set; }
    public ItemCooldown Cooldown { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public string? PreviewUrl { get; private set; }
    public ItemAvailability Availability => ItemAvailability.From(Enabled, Quantity);
    public ItemAvailability? PreviousAvailability { get; private set; }
    public DateTimeOffset? ProviderCreatedAt { get; private set; }
    public DateTimeOffset? ProviderUpdatedAt { get; private set; }

    /// <summary>
    /// When this item's data last actually moved — not when it was last looked at. A tick that
    /// finds nothing new leaves this alone, which is what stops a re-read of the whole catalogue
    /// every minute from rewriting every row it owns.
    /// <para>
    /// For an item that has dropped out of the upstream payload this is when it vanished. For one
    /// still present, "last seen" is the store's own <see cref="WatchedStore.LastSyncedAt"/>.
    /// </para>
    /// </summary>
    public DateTimeOffset LastChangedAt { get; private set; }

    public WatchedStore? WatchedStore { get; private set; }
    public WishlistEntry? WishlistEntry { get; private set; }

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    /// <summary>
    /// Applies a fresh upstream payload and raises an availability-transition event when the
    /// computed availability flipped. Events are dated from <c>data.LastFetchedAt</c> — the moment
    /// this payload was observed — so the entity never reads the clock itself.
    /// </summary>
    /// <returns>
    /// Whether the payload actually moved the item — answered here because this is the only place
    /// holding both the old and the new state.
    /// </returns>
    public StoreItemChange UpdateFromApi(StoreItemApiData data)
    {
        var change = Compare(data);
        var previousAvailability = Availability;
        ApplyData(data, previousAvailability);

        var currentAvailability = Availability;
        if (!previousAvailability.IsAvailable && currentAvailability.IsAvailable)
        {
            _domainEvents.Add(
                new StoreItemBecameAvailable(Id, WatchedStoreId, Name, data.LastFetchedAt)
            );
        }

        if (previousAvailability.IsAvailable && !currentAvailability.IsAvailable)
        {
            _domainEvents.Add(
                new StoreItemBecameUnavailable(Id, WatchedStoreId, Name, data.LastFetchedAt)
            );
        }

        if (change is StoreItemChange.Changed)
        {
            LastChangedAt = TruncateToStoredPrecision(data.LastFetchedAt);
        }

        return change;
    }

    /// <summary>
    /// Marks an item that vanished from the upstream store as unavailable. There is no API payload
    /// to date the transition from, so the caller supplies the observation time.
    /// </summary>
    /// <returns>
    /// Whether this actually took the item down. An item that was already unavailable is left
    /// untouched and reports no change.
    /// </returns>
    public StoreItemChange MarkUnavailable(DateTimeOffset now)
    {
        if (!Availability.IsAvailable)
        {
            return StoreItemChange.Unchanged;
        }

        Enabled = false;
        LastChangedAt = TruncateToStoredPrecision(now);
        _domainEvents.Add(new StoreItemBecameUnavailable(Id, WatchedStoreId, Name, now));

        return StoreItemChange.Changed;
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Mirrors <see cref="ApplyData"/> field for field, deciding whether the payload differs from
    /// what is already held. It covers every payload field but one: <c>LastFetchedAt</c> is a
    /// heartbeat, carrying a new instant on every fetch whether or not anything upstream moved, so
    /// counting it would report every item of every cycle as changed — the very problem this
    /// answers.
    /// <para>
    /// <see cref="PreviousAvailability"/> and <see cref="LastChangedAt"/> are absent for a different
    /// reason: both are entity-side <em>effects</em>, written only once this has decided a change
    /// happened. URLs and instants are compared in the form <see cref="ApplyData"/> stores them —
    /// see <see cref="NormalizeUrl"/> and <see cref="TruncateToStoredPrecision"/> — because comparing
    /// the raw payload reports a change forever on a value that merely arrives padded. A drift guard
    /// in the domain tests fails if a payload field is added without a case here.
    /// </para>
    /// </summary>
    private StoreItemChange Compare(StoreItemApiData data) =>
        Provider == data.Provider
        && ProviderItemId == data.ProviderItemId
        && StoreExternalId == data.StoreExternalId
        && Name == data.Name
        && Description == data.Description
        && Type == data.Type
        && Cost == data.Cost
        && CategoryName == data.CategoryName
        && Enabled == data.Enabled
        && Featured == data.Featured
        && Public == data.Public
        && SubscriberOnly == data.SubscriberOnly
        && AllowMessages == data.AllowMessages
        && UserInputDefinitions.SequenceEqual(data.UserInputDefinitions)
        && Sources.SequenceEqual(data.Sources)
        && Quantity == data.Quantity
        && Cooldown == data.Cooldown
        && ProviderCreatedAt == TruncateToStoredPrecision(data.ProviderCreatedAt)
        && ProviderUpdatedAt == TruncateToStoredPrecision(data.ProviderUpdatedAt)
        && ThumbnailUrl == NormalizeUrl(data.ThumbnailUrl)
        && PreviewUrl == NormalizeUrl(data.PreviewUrl)
            ? StoreItemChange.Unchanged
            : StoreItemChange.Changed;

    private void ApplyData(StoreItemApiData data, ItemAvailability previousAvailability)
    {
        Provider = data.Provider;
        ProviderItemId = data.ProviderItemId;
        StoreExternalId = data.StoreExternalId;
        Name = data.Name;
        Description = data.Description;
        Type = data.Type;
        Cost = data.Cost;
        CategoryName = data.CategoryName;
        Enabled = data.Enabled;
        Featured = data.Featured;
        Public = data.Public;
        SubscriberOnly = data.SubscriberOnly;
        AllowMessages = data.AllowMessages;
        UserInputDefinitions = data.UserInputDefinitions.ToArray();
        Sources = data.Sources.ToArray();
        Quantity = data.Quantity;
        Cooldown = data.Cooldown;
        ThumbnailUrl = NormalizeUrl(data.ThumbnailUrl);
        PreviewUrl = NormalizeUrl(data.PreviewUrl);
        PreviousAvailability = previousAvailability;
        ProviderCreatedAt = TruncateToStoredPrecision(data.ProviderCreatedAt);
        ProviderUpdatedAt = TruncateToStoredPrecision(data.ProviderUpdatedAt);
    }

    private static string? NormalizeUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.Trim();

    /// <summary>
    /// Drops the ticks a stored instant cannot carry, so what this entity holds is always what a
    /// reload would return. A <see cref="DateTimeOffset"/> counts 100ns ticks; the timestamp column
    /// it lands in keeps microseconds.
    /// <para>
    /// Applied on the way in, not only when comparing: a finer in-memory value makes
    /// <see cref="Compare"/> read two recordings of one instant as a change, and the change tracker
    /// then writes the row — so a tick that found nothing new still UPDATEs every item in the store.
    /// </para>
    /// </summary>
    private static DateTimeOffset TruncateToStoredPrecision(DateTimeOffset instant) =>
        new(instant.Ticks - (instant.Ticks % TimeSpan.TicksPerMicrosecond), instant.Offset);

    private static DateTimeOffset? TruncateToStoredPrecision(DateTimeOffset? instant) =>
        instant is null ? null : TruncateToStoredPrecision(instant.Value);
}
