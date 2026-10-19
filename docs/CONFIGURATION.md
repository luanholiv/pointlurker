# Configuration

Canonical reference for **every runtime configuration variable**. Other docs link here instead of re-listing variables.

---

## How configuration works

- **Environment variables only.** There is no base `appsettings.json`; `appsettings.Development.json` (dev connection string, logging, dev defaults) loads only under `ASPNETCORE_ENVIRONMENT=Development`.
- **Nested keys use `__`.** Section `Smtp` + property `Host` → `SMTP__HOST`. Binding is case-insensitive but does not split camelCase: `KeyRingPath` binds from `KEYRINGPATH`, not `KEY_RING_PATH`.
- **Validated on startup.** A missing or invalid required value logs a `Critical` message naming the setting and stops the backend.

**Default** is the code default (an Options-class initializer); for the connection string, logging levels and `ALLOWEDHOSTS`, which have no Options class, it is what `appsettings.Development.json` and `docker-compose.yml` set. **Required** means there is no code default.

---

## Backend

### Database

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `CONNECTIONSTRINGS__DATABASE` | For prod | `Host=localhost;Port=5432;Database=PointLurker;Username=api;Password=api;` | — | PostgreSQL connection string. The default applies under Development only. `docker-compose.yml` builds it from the `POSTGRES_*` variables. |

### StreamElements API

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `STREAMELEMENTS__BASEURL` | Outside Development | *(none in code)* | Valid URL | StreamElements API base URL. Compose and `appsettings.Development.json` both supply `https://api.streamelements.com`. |

### GauPoints API

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `GAUPOINTS__BASEURL` | No | `https://gaupoints.com.br` | Valid URL | GauPoints base URL. Override only for a mirror or test double. |
| `GAUPOINTS__SESSIONCOOKIENAME` | No | `kp_session` | `[A-Za-z0-9_-]{1,64}` | Cookie name the stored session is sent as, so an upstream rename needs no release. |

### Input resolution

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `INPUTRESOLUTION__MATCHTHRESHOLD` | No | `85` | `0`–`100` | FuzzySharp `TokenSetRatio` threshold for matching raw input labels to canonical ones, for every provider. Lower matches more aggressively. |

### Background sync jobs

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `SYNC__FETCHCRONEXPRESSION` | No | `* * * * *` | Valid cron | Store fetch schedule, for every provider. GauPoints is fetched at most every five minutes regardless. |
| `SYNC__USERNAMEREFRESHCRONEXPRESSION` | No | `0 0 * * *` | Valid cron | Provider account refresh: re-checks every stored credential, refreshes account and channel names and avatars, and sends credential expiry warnings. |
| `SYNC__MAXDEGREEOFPARALLELISM` | No | *(CPU count)* | `1`–`64` | Max stores fetched in parallel per sync run. |
| `SYNC__RESOLUTIONLOGRETENTIONDAYS` | No | `30` | min `1` | Days superseded input-resolution-log rows are kept; the newest row per store label always survives. |
| `SYNC__MINIMUMAUTOREDEEMINTERVALMINUTES` | No | `5` | `0`–`1440` | Minimum gap between two auto-redeems of one wishlist entry, applied as a floor under the item's own cooldown. Keep it at or above the fetch cadence. `0` disables it. |

### Provider credentials

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `CREDENTIALS__EXPIRYWARNINGHOURS` | No | `48` | `> 0` | How long before a credential's announced expiry (a GauPoints session's `exp`) the owner is emailed, once per expiry. Keep it longer than the account-refresh interval, or a credential can expire between two passes unwarned. |

### Redemption polling

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `REDEMPTIONS__POLLINGCRONEXPRESSION` | No | `* * * * *` | Valid cron | Schedule for polling outstanding redemptions for completion. |
| `REDEMPTIONS__COMPLETIONTIMEOUTHOURS` | No | `24` | `> 0` | Hours before a pending redemption is considered timed out. |
| `REDEMPTIONS__POLLFAILUREALERTTHRESHOLD` | No | `10` | `> 0` | Consecutive failed status checks on one redemption before the log escalates from warning to error. The redemption itself is never failed for it. |
| `REDEMPTIONS__FULFILLMENTSWEEPINTERVALMINUTES` | No | `5` | `> 0` | Gift-card delivery sweep interval; this is what retries a failed send. |
| `REDEMPTIONS__RETRYSWEEPINTERVALMINUTES` | No | `5` | `> 0` | Submission sweep interval; this is what lands a scheduled redemption retry. |

### Console URL

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `APP__PUBLICBASEURL` | No | *(none)* | Absolute `http`/`https` origin — scheme, host, optional port; **no trailing slash, path, query or fragment** | Browser-reachable address of this instance (with the shipped compose file, `https://` plus [`PUBLIC_HOST`](#docker-composeonly-variables)), used for links in notification emails. Unset, emails are sent without links. |

### Email / SMTP

The four required variables fail startup outside Development; under Development, placeholders let the backend boot and fail at send time instead. Provider recipes: [DEPLOYMENT.md → Email / SMTP](DEPLOYMENT.md#email--smtp).

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `SMTP__HOST` | Outside Development | *(none)* | Non-whitespace | SMTP server host (e.g. `smtp.gmail.com`). |
| `SMTP__USERNAME` | Outside Development | *(none)* | Non-whitespace | SMTP auth username, usually the full email address. |
| `SMTP__PASSWORD` | Outside Development | *(none)* | Non-whitespace | SMTP auth password. **Secret.** For Gmail, a 16-char App Password. |
| `SMTP__FROM` | Outside Development | *(none)* | Valid email | Sender address, optionally with a display name (`Monitor <you@gmail.com>`). Must match the authenticated mailbox or a verified alias. |
| `SMTP__PORT` | No | `587` | `1`–`65535` | `587` = STARTTLS; `465` = implicit TLS. |
| `SMTP__SECURITY` | No | `StartTls` | Any MailKit `SecureSocketOptions` name — **not validated** | TLS mode: `StartTls` with `587`, `SslOnConnect` with `465`; `Auto` and `None` also work. |
| `SMTP__MAXRETRIES` | No | `3` | `≥ 1` | Retries for transient send failures, on top of the first attempt. `0` is rejected. |
| `SMTP__RETRYBASEDELAYSECONDS` | No | `2` | `≥ 0` | Base delay for the exponential backoff between retries. |

### Authentication

App-session token lifetimes and the ASP.NET Identity account policy. See [AUTH.md](AUTH.md).

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `AUTHENTICATION__ACCESSTOKENEXPIRATIONMINUTES` | No | `30` | min `1`; **must be `<` refresh lifetime** | Access-token lifetime in minutes. |
| `AUTHENTICATION__REFRESHTOKENEXPIRATIONHOURS` | No | `24` | min `1`; **must be `>` access lifetime** | Refresh-token lifetime in hours. |
| `AUTHENTICATION__LOCKOUTDURATIONMINUTES` | No | `5` | min `1` | Lockout duration after too many failed sign-ins. |
| `AUTHENTICATION__LOCKOUTMAXFAILEDATTEMPTS` | No | `5` | min `1` | Failed sign-ins before lockout. |
| `AUTHENTICATION__PASSWORDMINIMUMLENGTH` | No | `8` | min `1` | Minimum password length; reaches the UI as the `min` param of `validation.password_too_short`. |

> **Binding note:** the five keys bind to two options classes, `AuthTokenOptions` and `IdentityPolicyOptions`. Every other Identity rule keeps the ASP.NET default (passwords need a digit, upper- and lowercase letters, a non-alphanumeric character).

### CORS

Off by default: nginx proxies `/api`, so the shipped stack is same-origin. Set it only when a browser on another origin must call the API directly. See [AUTH.md](AUTH.md).

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `CORS__ALLOWEDORIGINS__<n>` | No | *(empty)* | Absolute `http`/`https` origin: scheme + host + optional port, **no trailing slash**, no path/query/fragment, not `*` | Browser origins allowed cross-origin (`AllowAnyMethod` + `AllowAnyHeader`, never `AllowCredentials`). Empty means no CORS middleware at all. |

> **Binding note:** a list, so the env form is zero-indexed (`CORS__ALLOWEDORIGINS__0`, `__1`, …); a comma-separated `CORS__ALLOWEDORIGINS=a,b` is silently ignored. A malformed entry stops the boot; blank entries are ignored. Under Development an empty list falls back to `AllowAnyOrigin` for the Vite dev server; a non-empty list wins in every environment.

### Data Protection

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `DATAPROTECTION__KEYRINGPATH` | No | `/app/.aspnet/DataProtection-Keys` | Non-blank; resolvable path | Key-ring directory (encrypts provider credentials, redemption inputs and gift-card codes). **Persist and back it up** — losing it makes gift-card codes unrecoverable. Writability is not checked at startup; an unwritable path fails login with `500`. |

> **Binding note:** the section is one word, `DATAPROTECTION`. `DATA_PROTECTION__…` binds to a nonexistent key and is silently ignored.

### Logging & hosting

`docker-compose.yml` supplies these defaults; `appsettings.Development.json` carries them for local runs.

| Variable | Required | Default | Description |
|---|---|---|---|
| `LOGGING__LOGLEVEL__DEFAULT` | No | `Information` | Default minimum log level. |
| `LOGGING__LOGLEVEL__MICROSOFT` | No | `Warning` | Minimum level for `Microsoft.*` categories. |
| `ALLOWEDHOSTS` | No | `*` | Host-filtering allowlist. |
| `ASPNETCORE_ENVIRONMENT` | No | `Production` in the image | Host environment. `Development` loads `appsettings.Development.json` and relaxes CORS. |
| `ASPNETCORE_URLS` | No | `http://+:8080` in Compose | Addresses Kestrel binds. |
| `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` | No | `false` (set in `backend/Dockerfile`) | Must stay `false`: localized emails need ICU data. `scripts/verify-local.sh` asserts it. |

---

## Running under Docker Compose

Copy the template and fill in the four required `SMTP__*` values; `.env` is gitignored, so real secrets never reach a commit:

```bash
cp .env.example .env
docker compose up -d
```

[`.env.example`](../.env.example) lists only the variables `docker-compose.yml` reads. The optional ones are commented out at their defaults; uncomment one to change it.

`docker-compose.yml` forwards every backend variable above from `.env`, except:

- `CONNECTIONSTRINGS__DATABASE` — built from the `POSTGRES_*` variables below.
- `DATAPROTECTION__KEYRINGPATH` — pinned to the key-ring volume at `/app/.aspnet/DataProtection-Keys`.
- `SYNC__MAXDEGREEOFPARALLELISM` — no static default (CPU count); add it to the `backend` service's `environment:` to set it.
- `CORS__ALLOWEDORIGINS__<n>` — only slots `__0` and `__1` are wired; append `__2`, `__3`, … to the `backend` service's `environment:` for more.
- `ASPNETCORE_ENVIRONMENT`, `ASPNETCORE_URLS` — fixed by the compose file; `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT` by the image.

### Docker Compose–only variables

Read by `docker-compose.yml` itself, not the application.

| Variable | Required | Default | Description |
|---|---|---|---|
| `POSTGRES_USER` | No | `api` | PostgreSQL user; also fed into the backend connection string. |
| `POSTGRES_PASSWORD` | No | `api` | PostgreSQL password; also fed into the backend connection string. |
| `POSTGRES_DB` | No | `PointLurker` | PostgreSQL database name; also fed into the backend connection string. |
| `IMAGE_TAG` | No | `latest` | Image tag for backend and frontend (e.g. `v1.2.3`). |
| `PUBLIC_HOST` | No | `localhost` | Host name (no scheme or port) the TLS `proxy` serves on `443`; set it to the name other devices use to reach the server. Apply a change with `docker compose up -d --force-recreate proxy`. See [DEPLOYMENT.md § HTTPS](DEPLOYMENT.md#https). |

### E2E / test-harness variables

Read by `docker-compose.e2e.yml`, `scripts/verify-local.sh` and `frontend/playwright.config.ts` — never by the application. See [BUILD-AND-TEST.md](BUILD-AND-TEST.md).

| Variable | Required | Default | Description |
|---|---|---|---|
| `E2E_FRONTEND_PORT` | No | `8088` | Host port for the e2e frontend. |
| `E2E_MAILPIT_PORT` | No | `8026` | Host port for Mailpit's web API. |
| `CI_BASE_URL` | No | *(none)* | Base URL the `real` Playwright project drives. |
| `E2E_MAILPIT_URL` | No | *(none)* | Mailpit API the notification smoke reads. |
| `E2E_CHROMIUM_PATH` | No | *(none)* | Chromium executable override for Playwright. |

---

## Frontend

| Variable | Required | Default | Constraint | Description |
|---|---|---|---|---|
| `VITE_API_URL` | Dev only | `https://localhost:8080` (dev) | Valid URL | API base URL for the Vite dev server. Copy [`frontend/.env.example`](../frontend/.env.example) to `frontend/.env` (gitignored) and change the port if the backend listens elsewhere ([SETUP.md](SETUP.md)). Unset in production builds: the bundle uses the page origin and nginx proxies `/api`. |
