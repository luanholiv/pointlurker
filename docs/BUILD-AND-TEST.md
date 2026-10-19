# Build and Test

## Prerequisites

| Tool | Version | Purpose |
|---|---|---|
| .NET SDK | matching [`backend/global.json`](../backend/global.json) (≥ 10.0.400) | Backend build and test |
| Node.js | 22+ | Frontend build and test |
| Docker | any recent | Testcontainers (`IntegrationTests` only), E2E stack |

First-run setup (database container, dev certificate, where the API is served) is in [SETUP.md](SETUP.md); this document covers building and testing only.

---

## Backend

Working directory: `backend/`

### Build

```bash
dotnet build
```

### Run locally

```bash
dotnet run --project src/Presentation.WebApi
```

### Test

```bash
# Everything except the live probes (needs a Docker socket for IntegrationTests)
dotnet test --filter "Category!=LiveProbe&Category!=LiveProbeWrite"

# With JUnit XML report
dotnet test --filter "Category!=LiveProbe&Category!=LiveProbeWrite" --logger "junit;LogFilePath=./test-results/junit.xml"

# Exactly what CI runs: coverage, one JUnit XML per test assembly
dotnet test --filter "Category!=LiveProbe&Category!=LiveProbeWrite" \
  --collect:"XPlat Code Coverage" \
  --results-directory ./test-results \
  --logger "junit;LogFileName={assembly}.xml" \
  --settings coverage.runsettings

# HTML coverage report from the cobertura output
dotnet tool run reportgenerator \
  -reports:./test-results/**/coverage.cobertura.xml \
  -targetdir:./test-results/coverage-report \
  -reporttypes:HtmlInline_AzurePipelines
```

> To run everything at once — backend, frontend, UI and the real E2E against freshly built images — see [Full local verification](#full-local-verification-mirrors-ci).

Only `IntegrationTests` needs Docker: it shares **one** PostgreSQL and **one** Mailpit container across the assembly through `[Collection(ContainersCollection.Name)]`, resetting the database (Respawn) and the mailbox between tests. Never give a test class its own container — xunit constructs the class once per `[Fact]`, so that is one container per test.

> **Live probe:** `Category=LiveProbe` tests (`Infrastructure.ExternalServices.ContractTests`) hit the **real** StreamElements and GauPoints APIs. CI runs them weekly and on `workflow_dispatch` (`store-api-live-probe.yml`); the filters above exclude them. Run deliberately with `dotnet test --filter "Category=LiveProbe"`.

> **Write probe:** `Category=LiveProbeWrite` (`GauPointsRedeemLiveProbeTests`) sends a real `POST /api/redeem` to GauPoints, so it **never runs in CI** and is skipped unless `GAUPOINTS_PROBE_COOKIE` holds a live `kp_session` (the bare JWT). It posts only when an in-stock item costs more than the balance, then asserts the redeem was refused and the balance did not move; the cookie is never printed. From `backend/`:
>
> ```bash
> GAUPOINTS_PROBE_COOKIE='<kp_session>' dotnet test tests/Infrastructure.ExternalServices.ContractTests \
>   --filter "Category=LiveProbeWrite" --logger "console;verbosity=detailed"
> ```
>
> Record what it prints in [GAUPOINTS-API.md](GAUPOINTS-API.md) §3.1 and pin it with a contract test.

### CI publish (runtime artifact)

```bash
dotnet publish src/Presentation.WebApi/Presentation.WebApi.csproj \
  -c Release \
  --self-contained true \
  -r linux-musl-x64 \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o ./publish/amd64
```

`backend/Dockerfile` does `COPY ${TARGETARCH}/`, so the build context (`backend/publish`) must hold both `amd64/` and `arm64/`. CI publishes both as the `backend-publish` artifact and CD builds the images from it — see [CI-CD.md](CI-CD.md).

---

## Frontend

Working directory: `frontend/`

### Install dependencies

```bash
npm install
```

### Dev server

```bash
npm run dev
```

Runs at `http://localhost:5173`. Requires `VITE_API_URL` pointing at the backend — copy `frontend/.env.example` to `frontend/.env`.

### Build

```bash
npm run build
```

### Lint and format

```bash
npm run lint
npm run format:check
```

Prettier is a frontend-only tool. Backend C# belongs to CSharpier; `docs/`, `openspec/`, `.github/` and the root manifests have never been Prettier-formatted and must stay that way even when Prettier is run from the repo root. The root `.prettierignore` therefore denies everything and re-includes `frontend/`.

Two ignore files exist because Prettier resolves them relative to its cwd: lint-staged reads the root `.prettierignore`, `npm run format:check` runs from `frontend/` and reads `frontend/.prettierignore` (the root `.gitignore` does not reach there). **Keep their exclusions in sync** — if they disagree, staging a file reformats it behind the gate. Both exclude generated output (`coverage/`, `test-results/`, `playwright-report/`, `dist/`).

### Unit tests

```bash
# Single run
npm run test

# Watch mode
npm run test:watch

# With JUnit XML report (matches CI format)
npx vitest run --reporter=junit --outputFile.junit=test-results/junit.xml
```

---

## UI tests & E2E

Playwright runs two projects; **only `real` is end-to-end**:

- **`ui`** — route-mocked UI tests under `frontend/tests/ui/`: real browser and frontend bundle, every `/api` call stubbed with `page.route`. No backend, database or Docker.
- **`real`** — the unmocked E2E smoke under `frontend/tests/e2e/` (`*.real.spec.ts`), against the full Docker Compose stack with StreamElements faked by WireMock and mail caught by Mailpit.

### Default (UI tests, mocked API) — no Docker

```bash
cd frontend
npx playwright test --project=ui   # vite dev server starts automatically
```

`--project=ui` is not optional: a bare `npx playwright test` (and `npm run test:e2e`) also runs `real`, which fails without the stack below.

### Real smoke — full stack + WireMock SE

The E2E stack publishes only the frontend (`${E2E_FRONTEND_PORT:-8088}`) and Mailpit's web API (`${E2E_MAILPIT_PORT:-8026}`), so it runs alongside a dev stack; reach backend and database with `docker compose exec`.

> **`-p pl-e2e` is load-bearing.** Without it these commands join the dev project (`pointlurker`), recreate its containers, and `down -v` wipes your dev database.

```bash
# From repo root: e2e override (WireMock, Mailpit, one-minute sync) in its own compose project
IMAGE_TAG=ci docker compose -p pl-e2e -f docker-compose.yml -f docker-compose.e2e.yml up -d --wait

cd frontend
CI_BASE_URL=http://localhost:8088 CI=true E2E_MAILPIT_URL=http://localhost:8026 \
  npx playwright test --project=real --workers=1

# Teardown (wipes the pl-e2e DB volume — never the dev stack's)
docker compose -p pl-e2e -f docker-compose.yml -f docker-compose.e2e.yml down -v
```

JUnit XML output configured in `playwright.config.ts` → `test-results/junit.xml`.

#### What `docker-compose.e2e.yml` changes

It layers over `docker-compose.yml`; there is no separate CI compose file — the base file plus `IMAGE_TAG=ci` is the CI stack.

- **WireMock fakes StreamElements.** It serves `frontend/tests/e2e/wiremock` (mappings and `__files`) plus the contract-test fixture `store-items-response.json`, so both suites share one source of truth. The backend's `STREAMELEMENTS__BASEURL` points at it. The backend waits only for `service_started`: the WireMock image has no `curl`/`wget` for a healthcheck, and the backend calls it only mid-test.
- **Mailpit catches mail.** SMTP is required at startup, so the backend gets literal Mailpit settings (port `1025`, `SMTP__SECURITY=None`, any credentials accepted) that override the base file's empty `${SMTP__*}` — the stack boots without a host `.env`.
- **Sync runs every minute.** The availability notification has no other trigger (no manual sync endpoint), so the mail smoke needs the cron to fire. The other specs are unaffected: their payloads never change, so the delta-only upsert writes nothing.
- **Coexists with a dev stack.** The base file's `container_name` values are global, not project-scoped, so the override resets each to `null` and lets Compose auto-name; otherwise `up` fails with "container name … already in use". Host ports are non-default (`8088`, `8026`) so neither a dev frontend nor a dev Mailpit on `8025` is disturbed, and the TLS `proxy` is switched off (`profiles: ["disabled"]`) because a second stack binding `443` would collide.
- **Two published ports.** The frontend is published straight from nginx, which reaches the backend through the production `/api` proxy (no test-only nginx config). Mailpit's web API is published because the smoke reads the mailbox from the host, and nginx proxies only `/api`.

### Full local verification (mirrors CI)

`scripts/verify-local.sh` runs every suite except the live probes — backend, frontend unit, UI tests — then builds **fresh** `:local` images from your working tree and runs the real E2E smoke against them in the isolated `pl-e2e` project (ports `8088` and `8026` only, so a dev stack can stay up).

```bash
scripts/verify-local.sh                # everything (fresh images + real E2E)
scripts/verify-local.sh --skip-build   # reuse existing :local images for the real E2E
scripts/verify-local.sh --skip-real    # backend + frontend + UI tests only
```

To run the shipped stack itself, see [SETUP.md § 8](SETUP.md#8-running-the-full-docker-stack-optional) and [DEPLOYMENT.md](DEPLOYMENT.md).

---

## Security scans

```bash
# SAST (backend + frontend source)
snyk code test

# SCA (NuGet + npm dependencies)
snyk test
```

Requires `SNYK_TOKEN` set in environment.

### Dockerfile linting

```bash
# What the CI gate runs, on the exact binary it runs — the action pins this image by digest
docker run --rm -i ghcr.io/hadolint/hadolint:v2.15.1-debian hadolint - < backend/Dockerfile
docker run --rm -i ghcr.io/hadolint/hadolint:v2.15.1-debian hadolint - < frontend/Dockerfile
```

Hadolint runs with its defaults (no `.hadolint.yaml`), so info-level rules fail too. Not required to commit — the gate is the `hadolint` job in `ci.yml`; see [CI-CD.md](CI-CD.md).

### Secret scanning

```bash
# Full history — what the CI gate runs
gitleaks git . --redact --verbose --no-banner
```

Scans commit patches, so a secret committed and later deleted is still caught; `.gitleaks.toml` is auto-discovered, so a local run matches CI. Not required to commit — the gate is the `gitleaks` job in `ci.yml`; see [CI-CD.md](CI-CD.md).

---

## Environment variables

`dotnet run` needs none — see [SETUP.md](SETUP.md). Outside Development, `STREAMELEMENTS__BASEURL` and the four `SMTP__*` credentials are required and the backend fails fast without them. Every variable, default and constraint: [CONFIGURATION.md](CONFIGURATION.md).
