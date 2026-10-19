# CLAUDE.md

PointLurker — watches StreamElements and GauPoints loyalty stores, notifies on new items, auto-redeems wishlisted items. Backend: C# 14 / .NET 10 / ASP.NET Core. Frontend: React 19 / Vite / TypeScript / Tailwind v4. Database: PostgreSQL 17+.

## Project map

```
backend/src/   Core.Domain/ · Core.Application/ · Infrastructure.Database/ · Infrastructure.ExternalServices/ · Presentation.WebApi/
backend/tests/ {Core.Domain,Core.Application,Infrastructure.Database,Infrastructure.ExternalServices,Presentation.WebApi}.UnitTests · Infrastructure.ExternalServices.ContractTests · IntegrationTests · Architecture.Tests
frontend/src/  app · routes · features/{auth,stores,wishlist,redemptions} · components · hooks · lib · stores · config · assets · i18n · test · tests
frontend/tests/  ui/ (route-mocked UI tests, the `ui` Playwright project) · e2e/ (real-stack smoke, the `real` project)
frontend/eslint-rules/  repo-local ESLint rules (build tooling, outside every tsconfig)
```

## Documentation

Read `docs/ARCHITECTURE.md` for C4 diagrams, design decisions, quality attributes, technical constraints.
Read `docs/AUTH.md` for the auth model (Identity API, single-user, suppressed endpoints, token lifetimes, `/api/users/me`).
Read `docs/BUILD-AND-TEST.md` to build, run, or test backend, frontend, or E2E.
Read `docs/CI-CD.md` for why the pipeline is shaped as it is — job structure, scanning, image builds, action pinning, token scopes. Read it before editing `.github/workflows/**`.
Read `docs/CONFIGURATION.md` for the canonical catalog of all runtime env vars (backend + frontend), defaults, and constraints.
Read `docs/DEPLOYMENT.md` for Docker images, image provenance and signature verification, self-hosting, and rollback. The CI/CD pipeline itself is described in this file's CI/CD section and in `docs/SETUP.md`.
Read `docs/DESIGN.md` before changing frontend UI — type roles, colour tokens, radii. Text takes a role class (`t-hero`, `t-heading`, `t-subheading`, `t-small`, `t-meta`, `t-eyebrow`); never an inline `fontSize`/`fontFamily` (lint-enforced), and the display face is for headings only.
Read `docs/DOMAIN-MODEL.md` for aggregate map, class diagram, state machines, invariants, domain events.
Read `docs/ERROR-CODES.md` for the canonical reference of all backend error codes, params schemas, and frontend catalog keys.
Read `docs/GAUPOINTS-API.md` for GauPoints API endpoints and curl examples — an undocumented API, so each fact there is tagged observed or derived.
Read `docs/GLOSSARY.md` for ubiquitous language definitions.
Read `docs/I18N.md` for locale mechanics, catalog layout, formatters, and how to add a locale.
Read `docs/IDEA.md` for original project concept and motivation.
Read `docs/SETUP.md` for local dev environment setup.
Read `docs/STREAMELEMENTS-API.md` for SE API endpoints and curl examples.

## Domain rules

Domain code: `backend/src/Core.Domain/`. Read entity source files for property definitions.

**Bounded contexts:** Identity (`Identity/`) and Stores (`Stores/`).
**ApplicationUser:** Only one may exist. First `POST /api/auth/register` succeeds; subsequent calls return `400` (Identity `ValidationProblem` carrying `SingleUserOnly`). Enforced at the Identity core by `SingleUserValidator : IUserValidator<ApplicationUser>`, not an endpoint guard.
**ItemAvailability:** `IsAvailable = Enabled && (Quantity.Current == -1 || Quantity.Current > 0)` — `-1` = unlimited, MUST be available.
**StoreItem.UpdateFromApi:** Availability false→true raises `StoreItemBecameAvailable`. Inverse raises `StoreItemBecameUnavailable`.
**WishlistEntry:** At most one per `(UserId, StoreItemId)`. `DesiredRedemptions` bounds auto-redeem's total (`null` = unlimited, default `1`); `CanCreateRedemption` takes a `RedemptionPolicy` carrying the quota already held and a minimum interval that floors the item's cooldown — never give the entity a repository or provider knowledge. Meeting the quota leaves `AutoRedeem` on. `AutoRedeem` requires the item's `UserInputDefinitions` to be fully covered by the store's input profile; missing coverage blocks the entry (`IsBlockedOnInput`). **`WishlistEntry` raises no domain events** — unlike `StoreItem`, it has no event list at all; `BlockOnMissingInput`/`ClearInputBlock` only flip the flag, and `StoreInputCoverageUnit` *logs* each transition. The `WishlistEntryBlockedOnInput` / `WishlistEntryInputCoverageRestored` records exist but are never constructed, so do not write a handler expecting one.
**Store input profile:** Auto-redeem inputs resolve at the *store* level via `StoreInputAlias` / `StoreInputValue` — NOT per-entry values (the legacy `WishlistEntry.UserInputValues` was dropped). Coverage computed by `Core.Application/Stores/InputCoverage`.
**Redemption:** Append-only. Always created via `WishlistEntry.CreateRedemption()`, which snapshots item/store name, provider, store external id, provider item id, and the message (not input values — those resolve from the store profile at submission).

## Auth

**Mechanics — token lifetimes, the error contract, the suppression list, the frontend refresh flow — are in `docs/AUTH.md`. Read it before touching auth.** The rules that constrain code you write:

- Use ASP.NET Identity via `MapIdentityApi<ApplicationUser>()` under `/api/auth` — **write no custom auth code** beyond what `docs/AUTH.md` already lists.
- Framework Identity endpoints obey the same RFC 7807 `{code, params}` contract as every other endpoint, via `UseIdentityErrorContract` — never hand-roll a second error shape for them.
- **No roles** — authenticated == authorized. Protected endpoints use `RequireAuthorization()`. (The single-user invariant is under **Domain rules** above.)
- Endpoint suppression is terminal middleware (`UseSuppressedIdentityEndpoints`) and **must run before endpoint binding**.
- Current-user resource is `/api/users/me`; `GET /api/auth/bootstrap` is the anonymous first-run gate.
- Endpoint handlers resolve the caller via `ClaimsPrincipal.GetUserId()`, or `ISingleUserProvider` when the entity is needed — **never** `UserManager.GetUserAsync` (arch-test enforced). `401` belongs to `RequireAuthorization()`, so handlers carry no `401` branch.
- Token lifetimes are configurable and startup-validated (`AuthTokenOptions`, `Authentication` section); access must be `< refresh`.
- Provider secrets live in `ProviderCredential` (Identity context, one per `(UserId, Provider)`), encrypted with ASP.NET Data Protection under a per-provider purpose (`ProviderSecretProtectors`). **Existing `ProtectionPurposes` strings never change** — a changed string orphans every stored ciphertext.

## External services

- No web scraping — call each provider's API directly. See `docs/STREAMELEMENTS-API.md` and `docs/GAUPOINTS-API.md` for endpoints.
- Use Refit for HTTP clients. Every provider is consumed through the `IStoreProviderClient` port, picked by `StoreProvider` via `IStoreProviderClientFactory` — never the Refit `IStreamElementsApi` directly, and never one client injected straight into a use case (both arch-test enforced); resilience via `Microsoft.Extensions.Http.Resilience`.
- **A transport retry never covers a redeem.** Every provider's retry strategy calls `DisableForUnsafeHttpMethods()`: a redeem that answers 5xx may already exist upstream, so only `SubmitRedemptions`' counted, durable retry may repeat it.
- **GauPoints** (`Infrastructure.ExternalServices/GauPointsApi/`, see `docs/GAUPOINTS-API.md`) is one fixed store. Its `kp_session` travels as a `Cookie`, never `Authorization`; its JWT claims are read as hints (`DescribeCredential`), never verified. Classify its redeem on status plus `ok`, never on the Portuguese prose. Its synthesised input labels (`GauPointsInputs`) are prose shown to the owner, each mapped to one wire key in the same file.
- **Credential gates are per provider.** Use cases read a credential through `ProviderSecrets` for the provider the work belongs to; a missing or unreadable credential stops that provider's work only. Adding a store checks the credential of the provider being added (`auth.provider_credential_not_configured`, `provider` param). A single-store provider's identity is resolved server-side from `LookupStoreAsync`; a client-sent `channelId`/`displayName` for it is refused (`stores.identity_not_accepted`), never trusted. A provider's own wire (e.g. SE's "enough points" prose) is classified in its client, never in a use case.

## Background jobs

- Five hosted services (`Presentation.WebApi/HostedServices/`): store sync, redemption submission, redemption polling, provider account refresh (re-checks each stored credential, then emails a warning once per expiry for one about to expire — `WarnExpiringCredentials`, marker `ProviderCredential.ExpiryWarningSentFor`), gift-card delivery. Redemptions flow through a `Channel<Redemption>` producer/consumer.
- **A gift-card code is committed with its redemption, then mailed apart from it.** `SubmitRedemptions` stages the encrypted `FulfillmentCode` in the same unit of work as the redemption's outcome and signals `Channel<FulfillmentCodeReady>` only after the commit; `DeliverFulfillmentCodes` sweeps undelivered rows from the database. Every channel here is a wake-up signal, never the queue — never make one the only record of work. The code is a bearer secret: never logged, never in `LastError`, never returned from an endpoint. Rows are never pruned.
- Store sync commits each store independently (per-store checkpoint). No cursor/resume — each cron tick re-scans all active stores in parallel (`Parallel.ForEachAsync`).
- Cron expressions: `SYNC__FETCHCRONEXPRESSION` (store fetch) and `SYNC__USERNAMEREFRESHCRONEXPRESSION` (provider account refresh; the name predates providers and is kept so existing deployments need no config change).
- The username-refresh pass also runs `RefreshStoreChannelInfo` (store display names + avatars). Cosmetic channel data belongs on that slow schedule — the sync checkpoint must **not** look it up per tick.

## Notifications

- Notifications are sent over SMTP via MailKit (`IEmailNotifier` → `SmtpEmailService`). Config: `SMTP__HOST`, `SMTP__USERNAME`, `SMTP__PASSWORD`, `SMTP__FROM` (required outside Development) plus the optional tuning keys — catalogued in `docs/CONFIGURATION.md`, which is canonical; do not re-list them here. Transient send failures retry via Polly (transient-only); terminal failures record `Notification.MarkFailed`.

## Tests

- Mirror production code folder structure in unit test projects. Use TDD: write tests first, watch fail, then implement.
- Integration tests: Testcontainers + real PostgreSQL (requires Docker socket). E2E: full Docker Compose stack.
- **Docker belongs to `IntegrationTests` only** — every other test project must run without a Docker socket. Anything needing a container goes there, on `[Collection(ContainersCollection.Name)]`, which carries both the shared `DatabaseFixture` (Postgres, Respawn reset per test) and the shared `MailpitFixture` (SMTP sink, mailbox cleared per test). One collection, because a class can join only one and a second definition naming a fixture would start a second container rather than share the first. Never give a test class its own `PostgreSqlContainer`: xunit constructs the class once per `[Fact]`, so that is one container *per test* — slow, and it races under a parallel run.
- **JUnit XML** for all stacks. Backend: `JunitXml.TestLogger` in root `backend/Directory.Build.props`. Do NOT create `backend/tests/Directory.Build.props`. Use `--logger "junit;LogFilePath=./test-results/junit.xml"`.

## Backend conventions

- Configure all .NET projects to treat warnings as errors.
- NEVER suppress warnings or errors, ALWAYS fix them.
- Use NoTracking by default. Separate read/write models. Avoid N+1 queries.
- **Tracking is opted into, never out of.** `AppDbContext` is globally NoTracking, so never call `AsNoTracking()`. A repository read that returns tracked entities is named `…ForUpdateAsync` and is the only kind that calls `AsTracking()` — both directions are arch-test enforced (`InfrastructureConventionTests`).
- **Writes persist deltas, not whole rows.** No `DbSet.Update(...)`. A write path loads via `…ForUpdateAsync`, mutates, and commits — all **in the same DI scope**. Don't remove the `ValueComparer`s on the `jsonb` list columns — delta detection depends on them.
- **Repositories stage, `IUnitOfWork` commits.** Repository writes are synchronous (`Add`/`Update`/`Remove`/`AddRange`/`Upsert`) and touch only the change tracker; the use case calls `IUnitOfWork.SaveChangesAsync` once at its boundary. `Update` stages nothing — it asserts the entity is tracked and throws `InvalidOperationException` otherwise, because saving a detached entity writes nothing while looking like success. A `SaveChangesAsync` outside `UnitOfWork.cs` is arch-test enforced (`InfrastructureConventionTests`). Exception: `PruneAsync` uses `ExecuteDeleteAsync`, which bypasses the tracker and commits on its own.
- Two DbContexts: `AppDbContext` (application) and `IdentityDbContext` (auth).
- Organize DI registrations as `IServiceCollection` extension methods.
- **A registration is covered by resolving it, not by asserting it is present.** A factory-registered service (`AddScoped<T>(sp => …)`, a Refit client) is invisible to `ValidateOnBuild`, so a factory that throws is reported nowhere until the first request needs it. For an endpoint dependency that surfaces as a 500 raised during argument binding, before validation filters run. `CompositionRootTests` resolves every app-owned factory registration from a container built the way `Program.cs` builds it; anything added there must keep it passing.
- **No service locator in `Core.Application`** — take dependencies as constructor parameters; never `IServiceProvider`/`IServiceScopeFactory` or a call-time `GetRequiredService`. When a fresh scope per item is genuinely needed (parallel per-store sync), inject `Func<ScopedInstance<T>>`; only the layer's own DI registration touches the container. Arch-test enforced (`ApplicationConventionTests`).
- Use the Options pattern with `IValidateOptions` for strongly-typed settings validated on startup.
- **Request-shape validation is a FluentValidation validator per request contract** (`Presentation.WebApi/Endpoints/<Feature>/Validation/`), run by `ValidationEndpointFilter<TRequest>` — every route that binds a body opts in with `.ValidatesBody<TRequest>()` and every validator is registered in `AddRequestValidation()`. Each rule names its code with `.WithErrorCode(ErrorCodes.…)`; anything else is enforced by `Architecture.Tests/RequestValidationConventionTests` plus `Presentation.WebApi.UnitTests/Validation/RequestValidatorRegistrationTests` (the registration guard is a unit test, not an arch test — do not go looking for it under `Architecture.Tests`). **Never** use MVC's `InvalidModelStateResponseFactory` — it is a controller-only hook and cannot fire here. Domain invariants stay in the domain: a value object keeps its constructor guard as a backstop, and `Core.Domain`/`Core.Application` must not reference FluentValidation.
- **Query parameters travel in an `[AsParameters]` record**, one per endpoint (`Endpoints/<Feature>/Contracts/<Name>Query.cs`) — *including* endpoints that read a single parameter besides `CancellationToken`. Never bind a loose `string?` handler parameter; arch-test enforced (`QueryParameterConventionTests`). Every property is `string?`: a typed one hands parsing back to the model binder, which rejects `?offset=abc` during binding with a framework 400 that carries no `code` the frontend catalog can resolve. `SearchQuery` parses those raw strings, clamps `offset`/`limit`, and reports every unreadable parameter as a `params.errors[]` entry named by its query-string name. A query contract gets a validator only when it has a rule of its own — `stores/lookup` is the one, opting in with `.ValidatesQuery<TQuery>()`. `*Query` not `*Request`: the `*Request` suffix makes a registered validator mandatory.
- **The `validation.failed` envelope is built in one place** (`Validation/FieldErrorProblem.cs`) from `FieldError` entries. Three producers project into it: the validation filter (bodies and query contracts), `SearchQuery` (query filters), and `IdentityErrorMapper` (framework Identity endpoints). `ValidationFailureMapper`'s `ForwardedPlaceholders` allowlist governs the FluentValidation path only — it exists because FluentValidation exposes the rejected input, and one contract carries a provider credential.
- **Primary constructors for dependency injection** — use cases, repositories, hosted services, clients, initializers. Never copy a constructor parameter into a `private readonly` backing field. Enforced by `IDE0290` (`backend/.editorconfig`, `src/` only) and `Architecture.Tests/ConstructorStyleTests`.
- An explicit constructor is allowed only when the body must do more than assign parameters — the one case is `AppDbContext`, which sets `ChangeTracker.QueryTrackingBehavior`. A value *derived* from a parameter goes in a field initializer (`private readonly SmtpSettings _settings = settings.Value;`), not a constructor body.
- Core.Domain is exempt: entities keep a private EF constructor plus a public state-setting constructor, and value objects validate in theirs.

## i18n rules

See `docs/I18N.md` for locale mechanics, catalog layout, formatters, and how to add a locale. Hard rules:

- **Backend MUST NOT return localized strings in HTTP responses.** The `title` in ProblemDetails is always English (developer-facing); all user-facing rendering is frontend-side. Backend localizes only outbound emails.
- **Frontend never hardcodes user-facing strings.** Use `t()` for UI and `tError(code, params, title)` for error codes — all strings come from `frontend/src/i18n/locales/`.
- **Error codes come from `ErrorCodes.*` constants only** (`Core.Domain/Errors/ErrorCodes.cs`) — never inline a string code. See `docs/ERROR-CODES.md`.
- **Supported locales:** `en-US` (default) and `pt-BR`. Adding one touches both allowlists, both catalogs, and the email `.resx` files — see `docs/I18N.md`.

## Git / workflow

- Use conventional commits.
- **Comments never cite issue, PR, task or ticket numbers.** A comment says what the code guards and why; the number goes in the commit message and the PR body, which is where `git blame` already leads. Numbers rot — items get transferred, renumbered, closed as duplicates — and they do not survive a copy of the code. Enforced both ways: `local/no-tracker-refs` in `frontend/eslint.config.js` (runs in `npm run lint`, so it fires at pre-commit) and `Architecture.Tests/CommentConventionTests` over `backend/**` plus `backend/.editorconfig`. Both read comment text only, so test data such as a Discord tag is untouched. No allowlist and no inline opt-out — reword the comment. Prose under `docs/` and `README*` is out of scope: a narrative is where history belongs.
- **Comments never recount what the code did before, either.** State the rule the code holds; the shape it held before belongs in the commit message, for the same reason the number does. Enforced by `local/no-comment-history` and `Architecture.Tests/CommentHistoryConventionTests`, built exactly like the pair above. Deliberately narrow: only `used to` (passive voice excluded, across a wrapped line), `previously`, `the bug/defect/regression/problem this`, and `regression test for` are matched. `no longer`, `legacy` and a bare `now` read just as naturally as present-tense prose, so they are left alone — reword them by hand when they are an account of superseded code.
- **A hand-written dependency bump that clears a security advisory is `fix(deps)`, never `chore(deps)`.** Only a releasable commit reaches the published images, so a `chore` sits on `main` until some unrelated `feat`/`fix` happens to cut a release. Renovate's own advisory PRs already come through as `fix` — this is the rule for the ones a human writes.
- Trunk-based development: `main` is the trunk and always releasable. Never commit directly to `main`.
- Create a short-lived branch for every change (`feat/...`, `fix/...`, `chore/...`). Open a PR, merge back to `main` fast, delete the branch.

## CI/CD

**Rationale for every rule below lives in `docs/CI-CD.md`. Read it before editing `.github/workflows/**`.**

- **Three workflow files, by entry point:** `ci.yml` (push/PR to `main`), `cd.yml` (called by `ci.yml` + own `workflow_dispatch`), `store-api-live-probe.yml` (weekly cron, one step per provider). Nothing else is a reusable workflow. **`cd.yml` must never be renamed** — cosign bakes its path into the certificate identity that `docs/DEPLOYMENT.md` publishes.
- **CD is three jobs: `build` → `security-scan` → `publish`.** Only `publish` holds `id-token: write`. A skipped `build` must not skip `security-scan`. The middle job id is `security-scan`, not `scan`.
- **Snyk runs only in `security-scan`, never inside the jobs it gates**, one job for both stacks. Every `if:` inside it is step level, never job level.
- **`gitleaks` and `hadolint` are always-on top-level jobs in `ci.yml`, and `release` needs both.** No `if:`, no path filter. Those `needs:` edges are the whole enforcement — this plan has no required status checks. No `.hadolint.yaml`, no `# hadolint ignore`, no `.gitleaksignore`; fix findings at the source. `.gitleaks.toml` carries no allowlist; if a benign secret-shaped string ever reaches history, its exemption is scoped to those commit SHAs, never to a path or a literal.
- **`ui-tests` runs in `mcr.microsoft.com/playwright:v<version>-noble`; the tag must match `@playwright/test` in `frontend/package-lock.json`** (pinned exactly, guard step asserts it). `e2e-tests` cannot use that image — it needs a docker client and two host-published ports.
- **Every third-party action is pinned by commit SHA with the tag as a trailing comment** (`uses: actions/checkout@11d5960a… # v4`); a branch ref is never acceptable. The Snyk step also pins `snyk-version`, leading `v` included.
- **Every workflow declares its `GITHUB_TOKEN` scopes** — a `permissions:` block is a whitelist, so name every scope a job needs. Only `path-filter` (`pull-requests: read`) and `cd` (`packages: write` for GHCR, `id-token: write`) add a scope other than `contents`. A callee cannot ask for more than its caller granted, so `ci.yml`'s `cd` job must keep granting exactly what `cd.yml`'s `publish` declares.
- **`:latest` is created only after signing, never by a build.** Builds push `:v{version}` only; the arch-suffixed tags are unsigned intermediates.
- **A dependency bump only reaches the images if its commit is releasable** — advisory PRs arrive as `fix(deps): … [SECURITY]`, with a `.releaserc.json` backstop promoting `chore(deps)` carrying `[SECURITY]` to a patch.
- `backend/Dockerfile` / `frontend/Dockerfile` are runtime-only and **never built with their own directory as context** — the context is a staging dir of build output (`docker build -f backend/Dockerfile … backend/publish`). No dev backend Dockerfile; local dev is plain `dotnet run`, and there is no Aspire in this repository.
- **The shipped stack is HTTPS only.** TLS terminates at the Caddy `proxy` service in `docker-compose.yml` (inline `caddyfile` config, `tls internal` local CA, because no public CA issues for `.local`), and `443` is the only published port. Never publish `80`, `8080` or `5432` again and never add an HTTP→HTTPS redirect — `http://` is refused by design. The E2E overlay disables `proxy` and publishes the frontend on its own port. See `docs/DEPLOYMENT.md#https`.
- Frontend is built **origin-relative** (no `VITE_API_URL` at CD); CD ships the `frontend-dist` artifact E2E tested rather than rebuilding it.
- `release` skips CD when no commit is releasable. CD secret: `SNYK_TOKEN` only — images push to GHCR with `GITHUB_TOKEN`.

## Environment variables

**Canonical catalog: `docs/CONFIGURATION.md`** — every backend/frontend variable, default, and constraint. Do not re-list variables in other docs; link there.

Config is env-driven (`__` = nested key). There is no base `appsettings.json`; the tracked `appsettings.Development.json` holds the dev connection string, logging, and dev defaults for all option sections. Options classes validate on startup and the backend **fails fast** on a missing/invalid required value. Required with no code default: the four `SMTP__*` credentials (`HOST`/`USERNAME`/`PASSWORD`/`FROM`) and `STREAMELEMENTS__BASEURL`. Root `.env.example` covers the Docker Compose variables.
