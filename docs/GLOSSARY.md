# Glossary

The business vocabulary of the PointLurker domain, grouped by capability — one definition per term. Technical types (interfaces, DTOs, result/error types, config classes) are intentionally excluded; they live in the source and in [DOMAIN-MODEL.md](DOMAIN-MODEL.md), [ERROR-CODES.md](ERROR-CODES.md), and [CONFIGURATION.md](CONFIGURATION.md).

---

## Store monitoring

### WatchedStore
A loyalty store the user monitors, from any `StoreProvider`. Owns its `StoreItem`s and the input values + label aliases used for auto-redeem; can be activated or deactivated.

### StoreItem
A redeemable item inside a `WatchedStore`. Tracks availability, quantity, cooldown, and the input fields required to redeem it.

### ItemAvailability
Whether a `StoreItem` is currently redeemable, computed from `Enabled` + `ItemQuantity` (rule in [DOMAIN-MODEL.md](DOMAIN-MODEL.md#key-invariants)).

### ItemQuantity
`Total` and `Current` stock counts; `-1` means unlimited.

### ItemCooldown
Per-user, global, and per-category redemption cooldowns.

### StoreProvider
The upstream service a `WatchedStore`, its `StoreItem`s and its `Redemption`s belong to: `StreamElements` or `GauPoints`. Persisted as an int and append-only.

### GauPoints
The points store of one Kick streamer, and the second `StoreProvider`. One fixed store (`StoreExternalId` `gaupoints`), no messages, no redemption lookup, a fixed input form (`Full name`, `Phone`, `E-mail`, plus `Address` for anything shipped), and a `kp_session` cookie as its credential. See `docs/GAUPOINTS-API.md`.

### StoreExternalId
The provider-side identity of a `WatchedStore` — for StreamElements, the channel id; for GauPoints, the constant `gaupoints`. Stable across renames, and unique per user only together with the `StoreProvider`. The HTTP API still calls it `channelId`.

### ProviderItemId
The provider-side identifier of a `StoreItem`, carried onto a `Redemption` so the attempt can be traced upstream. The HTTP API calls it `providerItemId`.

### StoreItemBecameAvailable
Event: an item transitioned into an available state.

### StoreItemBecameUnavailable
Event: an item transitioned out of an available state.

_(Raisers and conditions for both events are in [DOMAIN-MODEL.md](DOMAIN-MODEL.md#domain-events).)_

---

## Wishlist & redemption

### WishlistEntry
The user's interest in a `StoreItem`. Toggles notify-on-available and auto-redeem, and may be blocked when required input values are missing.

### Redemption
A record of one redemption attempt for a `StoreItem`, created via `WishlistEntry.CreateRedemption()`. Progresses through a lifecycle from pending to a terminal outcome — states and transitions in [DOMAIN-MODEL.md](DOMAIN-MODEL.md#state-machines).

### FulfillmentCode
A gift-card code a provider returned for one redemption, stored encrypted, plus the state of emailing it to the owner. The provider returns it once, so it is committed with the redemption and kept indefinitely.

---

## Notifications

### Notification
A notification record grouping one or more `NotificationItem`s, tracking its delivery outcome (see [DOMAIN-MODEL.md](DOMAIN-MODEL.md#state-machines)). Its `NotificationChannel` decides what it is for.

### NotificationChannel
Where a `Notification` is destined. `Email` carries item availability and gift-card codes, and is the only channel actually delivered. `WebUi` records an auto-redeem entry becoming blocked on missing input; nothing currently reads those rows back, so they are written but never surfaced.

### NotificationItem
One store item inside a `Notification`, snapshotting its store and item name.

---

## Input resolution

### Raw Label
The input-field name as the provider reports it (e.g. `Minecraft Username`); resolution maps raw → canonical.

### Canonical Label
The normalized input-field name defined in the store profile (e.g. `minecraft_username`); what all values and resolution operate on.

### StoreInputValue
A user-provided input value for a canonical label, stored encrypted inside a `WatchedStore`.

### StoreInputAlias
A user-defined mapping from a raw label to a canonical label.

### Input Coverage
Whether every input field a `StoreItem` requires has a resolved value in the store profile; incomplete coverage blocks auto-redeem.

### StoreInputResolutionLogEntry
An audit row for one attempt to resolve a raw label to a canonical label; surfaces unresolved labels to the user.

### ResolutionSource
How a raw label was matched, recorded on each log entry: `Alias` (a user-defined mapping), `Exact` (whitespace-normalized, case-insensitive equality), `Fuzzy` (above the configured confidence threshold), or `Unresolved` (no match).

---

## Account

### ApplicationUser
The single registered user. Holds the `Locale` their UI and notification emails are rendered in, and no provider secret: those are `ProviderCredential`s, one per provider, each with the account name it resolves to upstream.
