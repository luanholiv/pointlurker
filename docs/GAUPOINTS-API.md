# GauPoints API Reference

This document contains the endpoint specifications and curl examples for the GauPoints API
required by this application.

GauPoints (the points store of the Kick streamer *Gaules*) publishes **no documentation and no
public API contract**. Everything below was recovered by probing the live endpoints and by reading
the site's client bundle. Every endpoint is therefore tagged:

- **Observed** — the request was made and the response recorded.
- **Derived** — the shape is taken from how the client bundle consumes it, but no response with
  that shape was captured. Treat these as the best available guess, not a contract.

Because the API is undocumented, nothing here is versioned or promised. Contract tests and the
scheduled live probe exist to catch it changing.

Last verified: **2026-09-18** for the anonymous endpoints, **2026-09-21** for the authenticated
ones. The only shape still unobserved is a *successful* redemption response, because observing it
means spending real points.

**Base URL:**
- `https://gaupoints.com.br`

**Authentication:**
- Session cookie: `Cookie: kp_session=<JWT>`

The stack is a Next.js App Router app behind Caddy behind Cloudflare. Responses carry
`x-content-type-options: nosniff`, `x-frame-options: SAMEORIGIN`,
`referrer-policy: strict-origin-when-cross-origin`, a `permissions-policy` and HSTS. **No route
returns any CORS header**, so a browser can only call these same-origin; server-to-server calls
are unaffected.

The complete surface the site's own client calls is five routes, plus `/api/auth/callback`, which
appears only as the OAuth `redirect_uri`:

| Route | `Allow` | Auth |
| --- | --- | --- |
| `GET /api/items` | `GET, HEAD, OPTIONS` | none |
| `GET /api/me` | `GET, HEAD, OPTIONS` | cookie |
| `POST /api/redeem` | `OPTIONS, POST` | cookie |
| `GET /api/auth/login` | `GET, HEAD, OPTIONS` | none |
| `GET /api/auth/callback` | — | none (OAuth return) |
| `POST /api/auth/logout` | `OPTIONS, POST` | cookie |

Any other method returns `405`. `OPTIONS` returns `204` with the `Allow` header shown above.

---

## 1. Store Items Endpoints

### 1.1 List Store Items

**Observed.** Lists the entire catalogue. Public endpoint — no auth required.

- Method: `GET`
- Uri: `https://gaupoints.com.br/api/items`
- Path Parameters: none — GauPoints is a single store with no channel concept
- Query Parameters: none

> **Query parameters are accepted and silently ignored.** `?type=physical&limit=1` returns a body
> byte-identical to the unparameterised call. The site's own type tabs filter client-side. Do not
> build a request expecting the server to narrow the result — it never will, and it will not tell
> you it did not.

> **There is no conditional-request path.** The response carries no `ETag`, no `Last-Modified` and
> no `Cache-Control`, and Cloudflare reports `cf-cache-status: DYNAMIC`. Every poll transfers the
> whole catalogue, so change detection has to happen locally after the fetch.

- Response body:
  - `items`: array. The whole catalogue, including items that are out of stock.
    - `id`: string. A human-readable slug (`netflix`, `boneg3x`), **not** a database id. Used as
      `itemId` in the redemption endpoint.
    - `name`: string.
    - `description`: string.
    - `cost`: int. Price in points.
    - `type`: string. One of `digital` or `physical`. Governs whether an address is required at
      redemption, and whether a code comes back.
    - `stock`: int. Remaining units. `0` means unavailable. There is **no unlimited sentinel** —
      GauPoints has no equivalent of the StreamElements `-1`.
    - `image`: string. Absolute URL. Every live item currently points at
      `https://cdn.streamelements.com/uploads/<id>.<ext>`, a leftover from the operators'
      migration off StreamElements.

> **`image` and `stock` may be absent.** All seven fields were present on all six live items, but
> the client guards both — it renders a placeholder when `image` is falsy, and wraps `stock` in
> `Number()` with a null check. Six items is a small sample; deserialize defensively.

**Example:**

Request:

```bash
curl -s "https://gaupoints.com.br/api/items"
```

Response:

200 OK

```json
{
  "items": [
    {
      "id": "netflix",
      "name": "Gift Card Netflix",
      "description": "Gift card Netflix no valor de R$ 35,00 (trinta e cinco reais)",
      "cost": 8000,
      "type": "digital",
      "stock": 9,
      "image": "https://cdn.streamelements.com/uploads/bf1099a1-1ed3-4a88-8aec-eb5b3bd3a03b.png"
    },
    {
      "id": "boneg3x",
      "name": "Boné G3X",
      "description": "Boné lendário do time do povo!!",
      "cost": 20000,
      "type": "physical",
      "stock": 0,
      "image": "https://cdn.streamelements.com/uploads/01k86bpb9jvxd0ty5sf0rd2nys.png"
    }
  ]
}
```

---

## 2. User & Points Endpoints

### 2.1 Get Current User

Returns the caller's session state and points balance. This is the only endpoint that reports a
balance; there is no separate points endpoint.

- Method: `GET`
- Uri: `https://gaupoints.com.br/api/me`
- Headers:
  - `Cookie`: `kp_session=<JWT>`. Optional — the endpoint answers `200` either way.
- Response body (signed out) — **Observed**:
  - `logged`: bool. `false`. No other key is present.
- Response body (signed in) — **Observed**:
  - `logged`: bool. `true`.
  - `points`: number. The caller's balance, at the top level — *not* nested under `user`.
  - `rank`: number. The caller's position on the `/ranking` leaderboard. Not read by the store
    page, so it does not appear anywhere in the client bundle — it is only visible by calling the
    endpoint with a session.
  - `user`: object.
    - `username`: string.
    - `name`: string.
    - `picture`: string. Absolute URL; Kick serves a default avatar when the user has none.

All three `user` fields were present on the observed response, but the store page reads them as
`n.user?.name || n.user?.username`, so treat the object and its members as nullable.

**Example:**

Request:

```bash
curl -s "https://gaupoints.com.br/api/me" \
  -H "Cookie: kp_session=<JWT>"
```

Response (signed out):

200 OK

```json
{ "logged": false }
```

Response (signed in). The shape is as observed; the values are illustrative, since a real one
carries the account holder's identity and balance:

200 OK

```json
{
  "logged": true,
  "user": {
    "username": "someviewer",
    "name": "someviewer",
    "picture": "https://kick.com/img/default-profile-pictures/default-avatar-5.webp"
  },
  "points": 1234,
  "rank": 567
}
```

---

## 3. Redemptions Endpoints

### 3.1 Redeem an item

Spends points on an item. **Irreversible.**

- Method: `POST`
- Uri: `https://gaupoints.com.br/api/redeem`
- Headers:
  - `Cookie`: `kp_session=<JWT>`. Mandatory.
  - `Content-Type`: `application/json`
  - `Idempotency-Key`: a client-generated key. The site's client generates one per dialog via
    `crypto.randomUUID()`. Deriving it from a durable identifier instead means a retry after a
    timeout cannot double-spend.
- JSON Body Parameters:
  - `itemId`: string. The `id` slug from the List Store Items endpoint. Mandatory.
  - `data`: object. The recipient details. Mandatory.
    - `fullName`: string. Mandatory.
    - `phone`: string. Mandatory.
    - `email`: string. Mandatory; the client validates it against `/^[^\s@]+@[^\s@]+\.[^\s@]+$/`.
    - `address`: string. Required only when the item's `type` is `physical`.

> **Always send all four `data` keys.** The client initialises its form state to
> `{fullName:"",phone:"",email:"",address:""}` and posts that object whole, so `address` travels as
> `""` for a non-physical item rather than being omitted. A three-key body has never been sent to
> this API and its handling is unknown.

- Response body — the failure envelope is **Observed**; the success fields are **Derived**:
  - `ok`: bool. Whether the redemption was accepted.
  - `error`: string. Present on failure. Human-readable, **in Portuguese**.
  - `message`: string, optional. Human-readable result on success.
  - `code`: string, optional. The gift-card code. Returned **once, in this response only** — there
    is no endpoint that reads it back. Only meaningful for items of type `digital`. The monitor
    commits it, encrypted, with the redemption's outcome and emails it afterwards (see
    `FulfillmentCode` in [DOMAIN-MODEL.md](DOMAIN-MODEL.md)).

A failure carries `error` and no `message`; the client renders `x.message || x.error` and shows
`x.code` in a separate block when present. The success shape cannot be observed without spending
real points, so `message` and `code` remain derived from that client code.

Observed failures so far:

| Condition | Status | Body |
| --- | --- | --- |
| No session cookie | `401` | `{"ok":false,"error":"Você precisa entrar com o Kick."}` |
| Unknown `itemId` | `400` | `{"ok":false,"error":"Item inválido."}` |

> **Errors are Portuguese prose, and the status code does not identify the cause.** An unknown
> item is a `400`, not a `404`. A consumer must not classify failures by matching these strings:
> they are user-facing copy, they will be reworded, and the set above is incomplete — the
> insufficient-points wording in particular has not been observed, because provoking it means
> spending points. Classify on `ok: false` plus the status, and keep a fallback bucket.
>
> The insufficient-points row is filled by the hand-run write probe, `GauPointsRedeemLiveProbeTests`
> (`Category=LiveProbeWrite`, see [BUILD-AND-TEST.md](BUILD-AND-TEST.md)). It redeems an in-stock
> item the balance cannot cover, chosen from the live catalogue and balance at run time, so it
> cannot spend points.

The session is checked before the item is resolved: an unauthenticated call with a nonexistent
`itemId` returns the `401`, not the `400`.

> **`ok: true` means accepted, not fulfilled.** See §5 — redemptions have a real upstream lifecycle
> (`processing` → `delivered`), and this call returns at its *start*.

**Example:**

Request:

```bash
curl -s -X POST "https://gaupoints.com.br/api/redeem" \
  -H "Cookie: kp_session=<JWT>" \
  -H 'Content-Type: application/json' \
  -H 'Idempotency-Key: <redemption-id>' \
  --data-raw '{"itemId":"netflix","data":{"fullName":"John Doe","phone":"+5511999999999","email":"test@email.com","address":""}}'
```

Response (unknown item) — **Observed**:

400 Bad Request

```json
{ "ok": false, "error": "Item inválido." }
```

---

## 4. Authentication Endpoints

GauPoints delegates identity to Kick via OAuth 2.0 with PKCE, using **its own** `client_id` and
`redirect_uri`. That flow cannot be executed by a third party: the only way to obtain a session is
to complete it in a browser and take the resulting cookie.

### 4.1 Begin sign-in

**Observed.**

- Method: `GET`
- Uri: `https://gaupoints.com.br/api/auth/login`
- Response: `307` to `https://id.kick.com/oauth/authorize` with `response_type=code`,
  `client_id=01KTPN89ZAKM955G0R4S94530N`, `redirect_uri=https://gaupoints.com.br/api/auth/callback`,
  `scope=user:read channel:read`, `code_challenge`, `code_challenge_method=S256` and `state`.
- Sets two short-lived cookies, both `Max-Age=600; Secure; HttpOnly; SameSite=Lax`:
  - `kp_verifier`: the PKCE code verifier.
  - `kp_state`: the CSRF state, matching the `state` query parameter.

The `client_id` is not a secret — it is sent to every anonymous visitor in this redirect.

### 4.2 OAuth callback

**Observed.**

- Method: `GET`
- Uri: `https://gaupoints.com.br/api/auth/callback`
- Query Parameters: `code` and `state`, supplied by Kick.
- Called without parameters it responds `307` to `/?erro=login`.
- On success it sets the session cookie `kp_session`.

### 4.3 The session cookie

`kp_session` is a JWT. It is a bearer credential for **cookie** authentication — send it as
`Cookie: kp_session=<value>`, never as an `Authorization` header.

Its payload carries `userId`, `username`, `name`, `picture`, `iat` and `exp`. Observed
`exp - iat` is **604800 seconds — exactly 7 days**.

> **Parse the payload, never verify it.** We are not the issuer and hold no key. `exp` is useful
> for failing early and warning ahead of expiry, and `username` seeds a display name without a
> round trip — but the contents are untrusted, and **`exp` is a lower bound on validity, not a
> guarantee**. A sign-out elsewhere, a password change or a revocation kills the cookie while its
> `exp` is still in the future, so liveness must still be confirmed against `GET /api/me`.

### 4.4 Sign out

**Observed.**

- Method: `POST`
- Uri: `https://gaupoints.com.br/api/auth/logout`
- Returns `401` without a session cookie.

---

## 5. Client-side vocabulary

These constants are declared in the site's shared bundle module and are **not** returned by any
endpoint. They are recorded because they describe states the API models internally.

Item type, and the labels the site shows for each:

| Value | Label |
| --- | --- |
| `digital` | `digital (entrega código)` |
| `physical` | `físico (envio)` |

Redemption status, with the site's labels:

| Value | Label |
| --- | --- |
| `processing` | `Processando` |
| `delivered` | `Entregue` |
| `pending_shipment` | `Aguardando envio` |
| `cancelled` | `Cancelado` |

A delivery mode of `direct` or `relay` is also declared, but is unused in the bundle and its
semantics are unknown.

The points unit is labelled `pontos`.

> **Redemptions have a lifecycle, and no endpoint exposes it.** The status vocabulary above proves
> a redemption moves `processing` → `delivered` (or `pending_shipment` for physical goods, or
> `cancelled`). The status is rendered only on the server-rendered `/minhas-trocas` page. A
> consumer of this API can therefore learn that a redemption was *accepted* and can never learn
> whether it was *fulfilled*.

---

## 6. What this API does not expose

- **No redemption lookup.** Nothing reads a redemption back by id; the redeem response has no id
  field for one. Redemption history and status live only on `/minhas-trocas`, which is
  server-rendered HTML with no JSON endpoint behind it.
- **No ranking endpoint.** `/ranking` is likewise server-rendered.
- **No store metadata.** There is no endpoint returning the store's name, avatar or currency name;
  a consumer must hold those as constants.
- **No channel or multi-store concept.** GauPoints is one store for one streamer.
- **No cooldowns, categories, messages, subscriber-only flags or per-item input definitions.** The
  redemption input schema is fixed across the catalogue and varies only by `type`.
- **No pagination.** `GET /api/items` returns everything.
- **No documented rate limits.** None were observed, and none were probed for. Poll conservatively
  and send an identifying `User-Agent`.

### A note on the site's own home page

The home page HTML can render "Nenhum item nesta categoria" while `GET /api/items` returns a full
catalogue. This is **not** a hidden server-side filter. The page is served from Next's full-route
cache (`cache-control: s-maxage=31536000, stale-while-revalidate`, `x-nextjs-cache: HIT`), so the
HTML is a long-cached empty shell and the grid is hydrated client-side from `/api/items`.

`GET /api/items` is the source of truth. Do not try to reproduce what the page appears to show.
