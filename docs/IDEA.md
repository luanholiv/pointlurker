# StreamElements Store Monitor (now PointLurker)

> **This is the original concept, kept as written.** It records what the project set out to
> build, not what it became — for the system as it actually is, read
> [ARCHITECTURE.md](ARCHITECTURE.md) and [DOMAIN-MODEL.md](DOMAIN-MODEL.md). Where the shipped
> behaviour diverged, a note below says so.

This web app wil be a simple StreamElements' (https://streamelements.com/) crawler to notify and automatically redeem newly added items to stores from various creators.
The main use case:

- user logs in (only works for a single logged in user)
- there will be a simple store administration page in order to add user's favorite streamers and some items from their stores to a wishlist
- the service will periodically fetch the favorited streamer's store and notify the users when an item is from the wishlist is available. User can configure the app to automatically redeem one or more items.

# 1. Epics

## 1.1. User Management

This application must always be protected behind proper authentication. user/password, 2fa optional. with a proper user page to change password/2fa.

There will be no sign up page. In the first run, if there is no user, you must let me sign up as the sole admin of the system. No confirmation email is necessary, let the user sign up, change password, 2fa, etc.

## 1.2. StreamElements' user token storage

User can insert the JWT token at the account settings page. This token will be manually obtained at StreamElements account page.
The system MUST store the token securely.

## 1.2. Store Management

There will be a store administration page in a web interface to add the stores to watch. When a store is added, the system must run one fetch in order to collect the user inputs needed to redeem the items. The system must show a form to fill all user input needed to redeem the items, that will be stored for further use. See the StreamElements' API details at ./STREAMELEMENTS-API.md

## 1.3. Wishlist

User can save items from a saved store to a wishlist. User can configure which items must be automatically redeemed. User can configure which items must be included in the notification about their availability.

## 1.4. Store items processing

The store items processing pipeline is the main use case. It runs based on the favorite stores saved by the user. 

### 1.4.1 Store items fetch

The system will periodically sync the items from StreamElements API to the internal storage for all stores saved by the user. All items from the store must be saved locally.

### 1.4.2 Store items check

After fetching the items, the system should check if the availability status of the item has changed from unavailable to available and save this information. IMPORTANT: items with current availability equals to -1 are also available. See the StreamElements' API details at ./STREAMELEMENTS-API.md

### 1.4.3 Notification

When a item is available, the system should check if the item is in a wishlist. If so, the system should notify the user via e-mail and web UI about the availability. The notification must be a single e-mail containing all items newly available from all the stores saved by the user grouped by store.

> **As shipped:** availability notifications go out by e-mail only. The `WebUi` notification
> channel exists, but it records auto-redeem entries becoming blocked on missing input, and no
> endpoint or screen reads those rows back.

### 1.4.4 Automatic redemption 

When an item is available, the system should check if the item is in a wishlist and is marked to be automatically redeemed. If so, the system should redeem the item. The redemption process will use the configured user input for each item. See the StreamElements' API details at ./STREAMELEMENTS-API.md

> **As shipped:** inputs are configured once per **store**, not per item. Each item's raw input
> labels are matched to the store's input profile by alias, then exact, then fuzzy resolution;
> the per-entry input values this line describes were dropped.

# 2. Tech Stack

- React and Tailwind CSS for web UI
- ASP.NET Core Web API for server-side processing and ASP.NET for authentication 
- PostgreSQL for data persistence
- Docker for containerization

# 3. Design 

Again, Tailwind, with light and dark themes, user can change through a top menu of sorts. Try to make it using proper HSL color science to choose the pallete but make the colors and visual identity similar to the StreamElements store website. No need to copy, just inspire by it.

# 4. Deployment

I will install this application as a docker compose stack in my home server, so I need a lean production ready docker configuration.