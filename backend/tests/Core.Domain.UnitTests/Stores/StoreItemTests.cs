using FluentAssertions;
using PointLurker.Core.Domain.Stores;
using PointLurker.Core.Domain.Stores.Enums;
using PointLurker.Core.Domain.ValueObjects;

namespace Core.Domain.UnitTests.Stores;

public sealed class StoreItemTests
{
    /// <summary>
    /// Fixed instant: the entity reads no clock. Availability events are dated from the payload's
    /// <c>LastFetchedAt</c>, or from the <c>now</c> handed to <see cref="StoreItem.MarkUnavailable"/>.
    /// </summary>
    private static readonly DateTimeOffset Now = new(2026, 3, 14, 15, 9, 26, TimeSpan.Zero);

    #region MarkUnavailable

    [Fact]
    public void MarkUnavailable_when_available_raises_StoreItemBecameUnavailable()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        item.ClearDomainEvents();

        item.MarkUnavailable(Now);

        item.Availability.IsAvailable.Should().BeFalse();
        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameUnavailable>();
    }

    [Fact]
    public void MarkUnavailable_when_available_with_unlimited_stock_raises_event()
    {
        var item = CreateItem(enabled: true, quantityCurrent: -1);
        item.ClearDomainEvents();

        item.MarkUnavailable(Now);

        item.Availability.IsAvailable.Should().BeFalse();
        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameUnavailable>();
    }

    [Fact]
    public void MarkUnavailable_when_already_unavailable_does_not_raise_event()
    {
        var item = CreateItem(enabled: false, quantityCurrent: 0);

        item.MarkUnavailable(Now);

        item.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Constructor — new item availability events

    [Fact]
    public void Constructor_available_item_raises_StoreItemBecameAvailable()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 10);

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameAvailable>();
    }

    [Fact]
    public void Constructor_available_item_with_unlimited_stock_raises_StoreItemBecameAvailable()
    {
        var item = CreateItem(enabled: true, quantityCurrent: -1);

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameAvailable>();
    }

    [Fact]
    public void Constructor_unavailable_item_does_not_raise_event()
    {
        var item = CreateItem(enabled: false, quantityCurrent: 0);

        item.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_disabled_item_with_stock_does_not_raise_event()
    {
        var item = CreateItem(enabled: false, quantityCurrent: 5);

        item.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_enabled_item_with_zero_stock_does_not_raise_event()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 0);

        item.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_sets_PreviousAvailability_to_unavailable_sentinel()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 10);

        item.PreviousAvailability.Should().NotBeNull();
        item.PreviousAvailability!.Value.IsAvailable.Should().BeFalse();
    }

    #endregion

    #region UpdateFromApi — event harvesting

    [Fact]
    public void UpdateFromApi_unavailable_to_available_raises_StoreItemBecameAvailable()
    {
        var item = CreateItem(enabled: false, quantityCurrent: 0);
        item.ClearDomainEvents();

        item.UpdateFromApi(CreateApiData(enabled: true, quantityCurrent: -1));

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameAvailable>();
    }

    [Fact]
    public void UpdateFromApi_available_to_unavailable_raises_StoreItemBecameUnavailable()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        item.ClearDomainEvents();

        item.UpdateFromApi(CreateApiData(enabled: true, quantityCurrent: 0));

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameUnavailable>();
    }

    [Fact]
    public void UpdateFromApi_same_state_does_not_raise_event()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        item.ClearDomainEvents();

        item.UpdateFromApi(CreateApiData(enabled: true, quantityCurrent: 3));

        item.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Event timestamps — sourced from the payload, never the clock

    [Fact]
    public void Constructor_dates_the_available_event_from_LastFetchedAt()
    {
        var fetchedAt = Now.AddDays(-4);

        var item = new StoreItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateApiData(enabled: true, quantityCurrent: 5, fetchedAt: fetchedAt)
        );

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameAvailable>()
            .Which.OccurredAt.Should()
            .Be(fetchedAt);
    }

    [Fact]
    public void UpdateFromApi_dates_the_available_event_from_LastFetchedAt()
    {
        var item = CreateItem(enabled: false, quantityCurrent: 0);
        item.ClearDomainEvents();
        var fetchedAt = Now.AddHours(9);

        item.UpdateFromApi(CreateApiData(enabled: true, quantityCurrent: -1, fetchedAt: fetchedAt));

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameAvailable>()
            .Which.OccurredAt.Should()
            .Be(fetchedAt);
    }

    [Fact]
    public void UpdateFromApi_dates_the_unavailable_event_from_LastFetchedAt()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        item.ClearDomainEvents();
        var fetchedAt = Now.AddHours(9);

        item.UpdateFromApi(CreateApiData(enabled: true, quantityCurrent: 0, fetchedAt: fetchedAt));

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameUnavailable>()
            .Which.OccurredAt.Should()
            .Be(fetchedAt);
    }

    [Fact]
    public void MarkUnavailable_dates_the_event_from_the_supplied_now()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        item.ClearDomainEvents();
        var observedAt = Now.AddMinutes(11);

        item.MarkUnavailable(observedAt);

        item.DomainEvents.Should()
            .ContainSingle()
            .Which.Should()
            .BeOfType<StoreItemBecameUnavailable>()
            .Which.OccurredAt.Should()
            .Be(observedAt);
    }

    #endregion

    #region Image URLs

    [Fact]
    public void Constructor_maps_image_urls()
    {
        var item = new StoreItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateApiData(
                enabled: true,
                quantityCurrent: 5,
                thumbnailUrl: "https://cdn.example.com/thumb.png",
                previewUrl: "https://cdn.example.com/preview.png"
            )
        );

        item.ThumbnailUrl.Should().Be("https://cdn.example.com/thumb.png");
        item.PreviewUrl.Should().Be("https://cdn.example.com/preview.png");
    }

    [Fact]
    public void UpdateFromApi_overwrites_image_urls()
    {
        var item = CreateItem(
            enabled: true,
            quantityCurrent: 5,
            thumbnailUrl: "https://cdn.example.com/old.png"
        );

        item.UpdateFromApi(
            CreateApiData(
                enabled: true,
                quantityCurrent: 5,
                thumbnailUrl: "https://cdn.example.com/new.png"
            )
        );

        item.ThumbnailUrl.Should().Be("https://cdn.example.com/new.png");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ApplyData_normalizes_blank_image_urls_to_null(string? blank)
    {
        var item = CreateItem(
            enabled: true,
            quantityCurrent: 5,
            thumbnailUrl: blank,
            previewUrl: blank
        );

        item.ThumbnailUrl.Should().BeNull();
        item.PreviewUrl.Should().BeNull();
    }

    [Fact]
    public void ApplyData_trims_image_urls()
    {
        var item = CreateItem(
            enabled: true,
            quantityCurrent: 5,
            thumbnailUrl: "  https://cdn.example.com/thumb.png  "
        );

        item.ThumbnailUrl.Should().Be("https://cdn.example.com/thumb.png");
    }

    [Fact]
    public void UpdateFromApi_image_only_change_raises_no_events()
    {
        var item = CreateItem(
            enabled: true,
            quantityCurrent: 5,
            thumbnailUrl: "https://cdn.example.com/old.png"
        );
        item.ClearDomainEvents();

        item.UpdateFromApi(
            CreateApiData(
                enabled: true,
                quantityCurrent: 5,
                thumbnailUrl: "https://cdn.example.com/new.png"
            )
        );

        item.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Change reporting

    /// <summary>
    /// Every <see cref="StoreItemApiData"/> field the entity persists. The fetch stamp is
    /// deliberately absent: it is a heartbeat, new on every fetch and backed by no observed data, so
    /// treating it as a change would make every item of every cycle look like it moved.
    /// </summary>
    private static readonly string[] PersistedPayloadFields =
    [
        nameof(StoreItemApiData.Provider),
        nameof(StoreItemApiData.ProviderItemId),
        nameof(StoreItemApiData.StoreExternalId),
        nameof(StoreItemApiData.Name),
        nameof(StoreItemApiData.Description),
        nameof(StoreItemApiData.Type),
        nameof(StoreItemApiData.Cost),
        nameof(StoreItemApiData.CategoryName),
        nameof(StoreItemApiData.Enabled),
        nameof(StoreItemApiData.Featured),
        nameof(StoreItemApiData.Public),
        nameof(StoreItemApiData.SubscriberOnly),
        nameof(StoreItemApiData.AllowMessages),
        nameof(StoreItemApiData.UserInputDefinitions),
        nameof(StoreItemApiData.Sources),
        nameof(StoreItemApiData.Quantity),
        nameof(StoreItemApiData.Cooldown),
        nameof(StoreItemApiData.ProviderCreatedAt),
        nameof(StoreItemApiData.ProviderUpdatedAt),
        nameof(StoreItemApiData.ThumbnailUrl),
        nameof(StoreItemApiData.PreviewUrl),
    ];

    public static TheoryData<string> PersistedPayloadFieldNames => [.. PersistedPayloadFields];

    [Theory]
    [MemberData(nameof(PersistedPayloadFieldNames))]
    public void UpdateFromApi_reports_Changed_when_a_persisted_field_differs(string field)
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        var payload = Mutate(
            CreateApiData(enabled: true, quantityCurrent: 5, fetchedAt: Now.AddMinutes(1)),
            field
        );

        item.UpdateFromApi(payload).Should().Be(StoreItemChange.Changed);
    }

    [Fact]
    public void UpdateFromApi_reports_Unchanged_when_only_the_fetch_stamp_moved()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        var fetchedAt = Now.AddMinutes(1);

        var change = item.UpdateFromApi(
            CreateApiData(enabled: true, quantityCurrent: 5, fetchedAt: fetchedAt)
        );

        change.Should().Be(StoreItemChange.Unchanged);

        // Deliberately NOT stamped. Writing a timestamp here is what made the sync rewrite every
        // row it owns on every tick — the dominant write in the whole database, for a value nothing
        // reads. The item having been seen is already recorded by the store's sync checkpoint.
        item.LastChangedAt.Should().Be(Now);
    }

    /// <summary>
    /// The comparison runs against the normalized form, not the raw payload — otherwise an upstream
    /// value that merely gained a trailing space would report a change on every cycle forever.
    /// </summary>
    [Fact]
    public void UpdateFromApi_reports_Unchanged_when_only_image_url_whitespace_differs()
    {
        var item = CreateItem(
            enabled: true,
            quantityCurrent: 5,
            thumbnailUrl: "https://cdn.example.com/thumb.png"
        );

        var change = item.UpdateFromApi(
            CreateApiData(
                enabled: true,
                quantityCurrent: 5,
                thumbnailUrl: "  https://cdn.example.com/thumb.png  "
            )
        );

        change.Should().Be(StoreItemChange.Unchanged);
    }

    /// <summary>
    /// A stored instant has been through a timestamp column that keeps microseconds, so it is
    /// coarser than the payload it came from. Two readings of the same instant that differ only
    /// below that resolution are the same instant, and must not report a change — otherwise every
    /// item reports as changed on every cycle, forever.
    /// </summary>
    [Fact]
    public void UpdateFromApi_reports_Unchanged_when_a_timestamp_differs_below_stored_precision()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);

        var change = item.UpdateFromApi(
            CreateApiData(enabled: true, quantityCurrent: 5) with
            {
                ProviderCreatedAt = Now.AddTicks(TimeSpan.TicksPerMicrosecond - 1),
            }
        );

        change.Should().Be(StoreItemChange.Unchanged);
    }

    [Fact]
    public void UpdateFromApi_reports_Changed_when_a_timestamp_moves_by_a_stored_tick()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);

        var change = item.UpdateFromApi(
            CreateApiData(enabled: true, quantityCurrent: 5) with
            {
                ProviderCreatedAt = Now.AddTicks(TimeSpan.TicksPerMicrosecond),
            }
        );

        change.Should().Be(StoreItemChange.Changed);
    }

    [Fact]
    public void MarkUnavailable_reports_Changed_when_the_item_was_available()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);

        item.MarkUnavailable(Now).Should().Be(StoreItemChange.Changed);
    }

    [Fact]
    public void MarkUnavailable_reports_Unchanged_when_the_item_was_already_unavailable()
    {
        var item = CreateItem(enabled: false, quantityCurrent: 0);

        item.MarkUnavailable(Now).Should().Be(StoreItemChange.Unchanged);
    }

    [Fact]
    public void Constructor_stamps_LastChangedAt_with_the_creation_instant()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);

        item.LastChangedAt.Should().Be(Now);
    }

    [Fact]
    public void UpdateFromApi_stamps_LastChangedAt_when_a_field_moved()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        var fetchedAt = Now.AddMinutes(1);

        var change = item.UpdateFromApi(
            CreateApiData(enabled: true, quantityCurrent: 5, fetchedAt: fetchedAt) with
            {
                Cost = 250,
            }
        );

        change.Should().Be(StoreItemChange.Changed);
        item.LastChangedAt.Should().Be(fetchedAt);
    }

    /// <summary>
    /// An item that drops out of the payload is taken down once, and that instant is the only record
    /// of when it vanished: the store-level sync checkpoint says when the store was last swept, not
    /// when this item stopped appearing in it.
    /// </summary>
    [Fact]
    public void MarkUnavailable_stamps_LastChangedAt_at_the_vanish_transition()
    {
        var item = CreateItem(enabled: true, quantityCurrent: 5);
        var vanishedAt = Now.AddHours(3);

        item.MarkUnavailable(vanishedAt);

        item.LastChangedAt.Should().Be(vanishedAt);
    }

    [Fact]
    public void MarkUnavailable_leaves_LastChangedAt_when_already_unavailable()
    {
        var item = CreateItem(enabled: false, quantityCurrent: 0);

        item.MarkUnavailable(Now.AddHours(3));

        item.LastChangedAt.Should().Be(Now);
    }

    /// <summary>
    /// The entity stores instants at the resolution the timestamp column keeps, so a reload cannot
    /// come back holding something different from what was assigned. Without this the row is dirty
    /// on every tick even when nothing moved: the payload carries 100ns ticks, the column keeps
    /// microseconds, and re-assigning the finer value makes the change tracker see a difference.
    /// </summary>
    [Fact]
    public void ApplyData_truncates_instants_to_what_the_timestamp_column_holds()
    {
        // 700ns past a whole microsecond — a value the column cannot represent.
        var jagged = Now.AddTicks(7);

        var item = new StoreItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateApiData(enabled: true, quantityCurrent: 5, fetchedAt: jagged) with
            {
                ProviderCreatedAt = jagged,
                ProviderUpdatedAt = jagged,
            }
        );

        item.ProviderCreatedAt.Should().Be(Now);
        item.ProviderUpdatedAt.Should().Be(Now);
        item.LastChangedAt.Should().Be(Now);
    }

    /// <summary>
    /// Drift guard. A field added to the payload and to <c>ApplyData</c> but left out of the change
    /// comparison would silently stop being counted. Failing here forces a case to be added, and
    /// that case then fails until the comparison covers the field too.
    /// </summary>
    [Fact]
    public void Every_persisted_payload_field_has_a_change_case()
    {
        var payloadFields = typeof(StoreItemApiData)
            .GetProperties()
            .Select(property => property.Name)
            .Except([nameof(StoreItemApiData.LastFetchedAt)]);

        PersistedPayloadFields.Should().BeEquivalentTo(payloadFields);
    }

    private static StoreItemApiData Mutate(StoreItemApiData data, string field) =>
        field switch
        {
            nameof(StoreItemApiData.Provider) => data with { Provider = StoreProvider.GauPoints },
            nameof(StoreItemApiData.ProviderItemId) => data with
            {
                ProviderItemId = new ProviderItemId("se-2"),
            },
            nameof(StoreItemApiData.StoreExternalId) => data with
            {
                StoreExternalId = new StoreExternalId("ch-2"),
            },
            nameof(StoreItemApiData.Name) => data with { Name = "Renamed Item" },
            nameof(StoreItemApiData.Description) => data with { Description = "Now described" },
            nameof(StoreItemApiData.Type) => data with { Type = "sound" },
            nameof(StoreItemApiData.Cost) => data with { Cost = 250 },
            nameof(StoreItemApiData.CategoryName) => data with { CategoryName = "sounds" },
            nameof(StoreItemApiData.Enabled) => data with { Enabled = false },
            nameof(StoreItemApiData.Featured) => data with { Featured = true },
            nameof(StoreItemApiData.Public) => data with { Public = false },
            nameof(StoreItemApiData.SubscriberOnly) => data with { SubscriberOnly = true },
            nameof(StoreItemApiData.AllowMessages) => data with { AllowMessages = true },
            nameof(StoreItemApiData.UserInputDefinitions) => data with
            {
                UserInputDefinitions = ["Steam ID"],
            },
            nameof(StoreItemApiData.Sources) => data with { Sources = ["twitch"] },
            nameof(StoreItemApiData.Quantity) => data with { Quantity = new ItemQuantity(-1, 4) },
            nameof(StoreItemApiData.Cooldown) => data with
            {
                Cooldown = new ItemCooldown(60_000, 0, 0),
            },
            nameof(StoreItemApiData.ProviderCreatedAt) => data with
            {
                ProviderCreatedAt = Now.AddDays(-1),
            },
            nameof(StoreItemApiData.ProviderUpdatedAt) => data with { ProviderUpdatedAt = Now },
            nameof(StoreItemApiData.ThumbnailUrl) => data with
            {
                ThumbnailUrl = "https://cdn.example.com/thumb.png",
            },
            nameof(StoreItemApiData.PreviewUrl) => data with
            {
                PreviewUrl = "https://cdn.example.com/preview.png",
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(field),
                field,
                "No mutation defined for this payload field."
            ),
        };

    #endregion

    #region Helpers

    private static StoreItem CreateItem(
        bool enabled,
        int quantityCurrent,
        string? thumbnailUrl = null,
        string? previewUrl = null
    )
    {
        return new StoreItem(
            Guid.NewGuid(),
            Guid.NewGuid(),
            CreateApiData(enabled, quantityCurrent, thumbnailUrl, previewUrl)
        );
    }

    private static StoreItemApiData CreateApiData(
        bool enabled,
        int quantityCurrent,
        string? thumbnailUrl = null,
        string? previewUrl = null,
        DateTimeOffset? fetchedAt = null
    )
    {
        return new StoreItemApiData(
            StoreProvider.StreamElements,
            new ProviderItemId("se-1"),
            new StoreExternalId("ch-1"),
            "Test Item",
            null,
            "perk",
            100,
            "default",
            enabled,
            false,
            true,
            false,
            false,
            [],
            [],
            new ItemQuantity(-1, quantityCurrent),
            new ItemCooldown(0, 0, 0),
            Now,
            null,
            fetchedAt ?? Now,
            thumbnailUrl,
            previewUrl
        );
    }

    #endregion
}
