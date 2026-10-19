# StreamElements API Reference

This document contains the endpoint specifications and curl examples for the StreamElements API required by this application.

**Base URLs:**
- V2: `https://api.streamelements.com/kappa/v2`

**Authentication:**
- JWT Token: `Authorization: bearer <TOKEN>`

---

## 1. Store Items Endpoints

### 1.1 List Store Items
Lists all store items for a specific channel. Public endpoint - no auth required.

- Method: `GET`
- Uri: `https://api.streamelements.com/kappa/v2/store/{channel}/items`
- Headers: 
  - `Content-Type`: the content type
- Path Parameters:
  - `channel`: the channel identifier
- Query Parameters:
  - `source`: Filter by redemption source - `website`, `bot`, `extension`, `all` (default: `all`). Optional, always send `website`
- Response body:
  - `bot`: The bot configuration that describes bot behavior. 
    - `identifier`: string, optional. The bot identifier
    - `sendResponse` bool, optional. The bot automated message behavior after a redemption
  - `cooldown`: The cooldown object that describes the wait time to redeem items sequentially
    - `user`: long. Indicates how long the user must wait to redeem this specific item in milliseconds.
    - `global`: long. Indicates how long all users must wait to redeem this specific item in milliseconds.
    - `category`: long. Indicates how long the user must wait to redeem any item from this category in milliseconds.
  - `quantity`: The quantity object that describes the item's availability
    - `total`: int. Indicates the total number of initial available items
    - `current`: int. Indicates the current number of available items
  - `alert`: Configures the behavior of alerts after a redemption
    - `enabled`: bool, optional. Indicates if an alert should be played
    - `audio`: Audio configuration for the alert
      - `volume`: float, optional. Indicates the audio volume
      - `src`: string, optional. Indicates the audio source
    - `graphics`: Visual configuration for the alert
      - `duration`: int, optional. Indicates the alert display time 
      - `type`: string, optional. Indicates the display type, like image, gif, video 
      - `src`: string, optional. Indicates the display source 
  - `_id`: string. The item's identifier; will be used in redemption endpoint
  - `subscriberOnly`: bool. 
  - `sources`: string[]. 
  - `userInput`: string[]. The needed user input to redeem the item, where each item is the description of a field; will be used in redemption endpoint
  - `enabled`: bool.
  - `featured`: bool.
  - `name`: string.
  - `description`: string, optional.
  - `thumbnail`: string, optional. URL to the item's thumbnail image hosted on the SE CDN (`https://cdn.streamelements.com/uploads/<uuid>.<ext>`). Used as the item card image in the SE store UI. Present on most items; may be `null`.
  - `preview`: string, optional. URL to the item's larger preview image hosted on the SE CDN. Set less frequently than `thumbnail`; may be `null`. Observed formats: `png`, `jpg`, `gif`.
  - `type`: string.
  - `cost`: long.
  - `allowMessages`: bool.
  - `categoryName`: string.
  - `public`: bool.
  - `channel`: string. The channel's identifier; will be used in redemption endpoint
  - `createdAt`: DateTimeOffset.
  - `updatedAt`: DateTimeOffset, optional.
  - `order`: int. The item's display order in the SE store. Present on every observed item; not consumed by this client.

**Example:**

Request: 

```bash
curl -X GET "https://api.streamelements.com/kappa/v2/store/5b2e2007760aeb7729487dab/items?source=website"
```

Response: 

200 OK

```json
[
  {
    "bot": {
      "identifier": "testitem",
      "sendResponse": false
    },
    "cooldown": {
      "user": 5,
      "global": 0
    },
    "quantity": {
      "total": -1
    },
    "alert": {
      "graphics": {
        "duration": 8
      },
      "audio": {
        "volume": 0.5,
        "src": null
      },
      "enabled": true
    },
    "_id": "5c0bb2aede9a4c4402a15056",
    "subscriberOnly": false,
    "userInput": ["email", "name"],
    "enabled": true,
    "featured": false,
    "name": "Test item",
    "description": "Test description",
    "thumbnail": "https://cdn.streamelements.com/uploads/f57c97ca-488c-4e34-883f-18b264ed7428.png",
    "preview": "https://cdn.streamelements.com/uploads/3f202956-177a-41e2-a171-632cf43de496.png",
    "type": "perk",
    "cost": 1,
    "allowMessages": false,
    "categoryName": "skin", 
    "public": false,
    "channel": "5b2e2007760aeb7729487dab",
    "createdAt": "2018-12-08T12:01:50.208Z",
    "updatedAt": "2018-12-08T12:02:47.902Z"
  }
]
```

---

## 2. Channels Endpoints

### 2.1 Lookup Channel by Username or Channel ID

Resolves a Twitch username (or a StreamElements channel ID) to a StreamElements channel ID, display name, and avatar. Public endpoint — no auth required.

- Method: `GET`
- Uri: `https://api.streamelements.com/kappa/v2/channels/{channel}`
- Path Parameters:
  - `channel`: the Twitch username (e.g. `xqc`) or the StreamElements channel ID (e.g. `5cc799026e852d26fcf16717`); both are accepted
- Response body:
  Only `_id`, `username`, `displayName` and `avatar` are bound by `ChannelLookupResponse`; the rest are observed on the wire but ignored. Unlike the store endpoints above, this one has no contract test or WireMock fixture, so the shape below is from live observation only.

  - `_id`: string. The StreamElements channel identifier. *(consumed)*
  - `username`: string. The Twitch username. *(consumed)*
  - `displayName`: string. The display name as shown on StreamElements. *(consumed)*
  - `avatar`: string, **nullable**. URL to the channel avatar image. *(consumed)*
  - `provider`: string. The streaming platform (e.g. `twitch`).
  - `providerId`: string. The platform-specific user ID.
  - `suspended`: bool.
  - `inactive`: bool.

**Example:**

Request:

```bash
curl -X GET "https://api.streamelements.com/kappa/v2/channels/xqc"
```

Response:

200 OK

```json
{
  "_id": "5b62356e6f66ad3de89066a7",
  "username": "xqc",
  "displayName": "xQc",
  "provider": "twitch",
  "providerId": "71092938",
  "avatar": "https://static-cdn.jtvnw.net/jtv_user_pictures/...",
  "suspended": false,
  "inactive": false
}
```

404 Not Found — username does not exist on StreamElements.

---

## 3. Redemptions Endpoints

### Redeem an item

Redeems an item to the user account.

- Method: `POST` 
- Uri: `https://api.streamelements.com/kappa/v2/store/{channel}/redemptions/{item}`
- Headers: 
  - `Authorization`: the authentication bearer token
  - `Content-Type`: the content type
- Path Parameters:
  - `channel`: the channel identifier
  - `item`: the item identifier
- JSON Body Parameters:
  - `input`: a `string` array, where each item is the response to `userInput` array from the List Store Items endpoint. E.g.: if `userInput` is `["email", "name"]`, then `input` MUST BE `["test@email.com", "John Doe"]`. Mandatory.
  - `message`: a `string` that contains a user message, sent when `allowMessages` from List Store Items endpoint is true. Optional. This client serializes it with `JsonIgnore(WhenWritingNull)`, so a null message **omits the key entirely** while an empty string is sent as `""`.

**Example:**

Request:

```bash
curl -X POST "https://api.streamelements.com/kappa/v2/store/{channel}/redemptions/{item}"
  -H 'Authorization: Bearer <TOKEN>'
  -H 'Content-Type: application/json'
  --data-raw $'{"input":["test@email.com","John Doe"],"message":""}'
```

Response:

200 OK

``` json
{
  "channel":"5ea766d578edd899791c1eb8",
  "redeemerType":"Channel",
  "redeemer":"5fa01b6721737924e36aa7f7",
  "item":"5ecede07db1d069c8feacee2",
  "completed":false,
  "source":"website",
  "input":[ "test@email.com", "John Doe" ],
  "message":"",
  "_id":"69a595406f7bf684bcf63750",
  "createdAt":"2026-03-02T13:48:48.043Z",
  "updatedAt":"2026-03-02T13:48:48.043Z"
}
```

### Find a redemption by ID

Finds a single redemption by its ID.

> **This response is not the same shape as the redeem response above.** The lookup expands `item`
> and `redeemer` into objects, adds `rejected`, and omits `message`; the redeem call returns `item`
> and `redeemer` as bare id strings and includes `message`. One shared contract therefore
> deserializes for exactly one of the two endpoints — which is why `GetRedemptionResponse` is a
> separate type from `RedemptionResponse`, and why the WireMock fixtures for the two must not be
> copied from one another.

- Method: `GET`
- Uri: `https://api.streamelements.com/kappa/v2/store/{channel}/redemptions/{redemption}`
- Headers: 
  - `Authorization`: the authentication bearer token
  - `Content-Type`: the content type
- Path Parameters:
  - `channel`: the channel identifier
  - `redemption`: the redemption identifier

**Example:**

Request:

```bash
curl -X GET "https://api.streamelements.com/kappa/v2/store/{channel}/redemptions/{redemption}"
  -H 'Authorization: Bearer <TOKEN>'
  -H 'Content-Type: application/json'
```

Response:

200 OK

```json
{
	"_id": "69d54da153a6da3f2065bdc4",
	"channel": "5fa01b6721737924e36aa7f7",
	"redeemerType": "Channel",
	"redeemer": {
		"_id": "5fa01b6721737924e36aa7f7",
		"avatar": "https://static-cdn.jtvnw.net/jtv_user_pictures/b998defd-863b-4ea7-913c-f96e91af7ce8-profile_image-300x300.png",
		"username": "luanholiv",
		"inactive": false,
		"isPartner": true
	},
	"item": {
		"_id": "69d40324fdad9ecb1e51b763",
		"name": "AK-47 | Slate",
		"userInput": [
			"Trade link",
			"Confirma?"
		]
	},
	"completed": true,
	"source": "website",
	"input": [
		"teste",
		"sim"
	],
	"createdAt": "2026-04-07T18:32:01.253Z",
	"updatedAt": "2026-04-07T19:08:57.813Z",
	"rejected": false
}
```

---

## 4. Points & User Endpoints

Used by the affordability gate: auto-redeem only proceeds when the owner has enough points in the target channel.

### 4.1 Get Current User

Returns the authenticated user. Used to resolve the owner's `username` (needed by the points endpoint). Requires auth.

- Method: `GET`
- Uri: `https://api.streamelements.com/kappa/v2/users/current`
- Headers:
  - `Authorization`: the authentication bearer token
- Response body (only `username` is consumed; other fields ignored):
  - `username`: string. The owner's StreamElements username.

**Example:**

Request:

```bash
curl -X GET "https://api.streamelements.com/kappa/v2/users/current" \
  -H 'Authorization: Bearer <TOKEN>'
```

Response:

200 OK

```json
{
  "_id": "5fa01b672173798fc06aa7f6",
  "primaryChannel": "5fa01b6721737924e36aa7f7",
  "username": "luanholiv",
  "channels": [{ "_id": "5fa01b6721737924e36aa7f7", "provider": "twitch", "username": "luanholiv" }]
}
```

### 4.2 Get Channel Points for a User

Returns the user's loyalty point balance for a specific channel. Per `(channel, username)`. Requires auth.

- Method: `GET`
- Uri: `https://api.streamelements.com/kappa/v2/points/{channel}/{username}`
- Headers:
  - `Authorization`: the authentication bearer token
- Path Parameters:
  - `channel`: the channel identifier
  - `username`: the viewer's username (from `users/current`)
- Response body (only `points` is consumed):
  - `points`: integer. The current point balance for this user in this channel.

**Example:**

Request:

```bash
curl -X GET "https://api.streamelements.com/kappa/v2/points/{channel}/{username}" \
  -H 'Authorization: Bearer <TOKEN>'
```

Response:

200 OK

```json
{
  "channel": "5cc799026e852d26fcf16717",
  "username": "luanholiv",
  "points": 249406,
  "pointsAlltime": 307732,
  "watchtime": 513480,
  "rank": 7434
}
```

> **Insufficient points on redeem:** the Redeem endpoint (§3) returns `400 Bad Request` with body
> `{"statusCode":400,"error":"Bad Request","message":"Sorry you don't have enough points"}` when the
> balance is too low. `StreamElementsApiClient` recognises that wording and answers
> `ProviderRedemptionResult.InsufficientPoints`, which the use case records as the `InsufficientPoints`
> failure code (permanent, no retry). The wording is read in the client and nowhere else, so any
> other `400` stays a generic `ProviderApiBadRequest`.