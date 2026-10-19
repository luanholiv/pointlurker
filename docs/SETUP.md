# Dev Environment Setup

Step-by-step guide for setting up a local development environment from scratch.

---

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | see [`backend/global.json`](../backend/global.json) | `rollForward: latestMinor` only rolls *forward*: an older feature band fails with `A compatible .NET SDK was not found`. Download from [dot.net](https://dotnet.microsoft.com/download). |
| Node.js | **22+** (`engines` in `frontend/package.json`) | Use [nvm](https://github.com/nvm-sh/nvm) or [fnm](https://github.com/Schniz/fnm). |
| Docker Desktop | any recent | PostgreSQL for local dev, Testcontainers, E2E stack. |
| Git | any | — |

Verify:

```bash
(cd backend && dotnet --version)   # must satisfy the backend/global.json pin
node --version                    # v22.x or v24.x
docker info                       # must be running
```

---

## 1. Clone

```bash
git clone <repo-url>
cd pointlurker
```

---

## 2. Install root dependencies

From the repo root:

```bash
npm install
```

Its `prepare` script:
- installs the husky pre-commit hook (lint-staged, slopwatch, Prettier, ESLint, TypeScript check);
- restores the local dotnet tools (`dotnet-ef`, `csharpier`, `slopwatch`, `reportgenerator`).

> `npm install` inside `frontend/` does **not** run it, so no hooks.

---

## 3. Database

Start only PostgreSQL:

```bash
docker compose up database -d
```

Default credentials match `backend/src/Presentation.WebApi/appsettings.Development.json`:

| Setting | Value |
|---|---|
| Host | `localhost:5432` |
| Database | `PointLurker` |
| Username | `api` |
| Password | `api` |

The backend applies EF Core migrations on startup.

---

## 4. Backend

### 4a. Restore local tools

Step 2 already did this. To restore by hand:

```bash
cd backend
dotnet tool restore
```

Versions are pinned in [`backend/.config/dotnet-tools.json`](../backend/.config/dotnet-tools.json):

| Tool | Purpose |
|---|---|
| `dotnet-ef` | EF Core migrations |
| `csharpier` | Code formatter (enforced in CI) |
| `slopwatch` | Code quality linter |
| `reportgenerator` | Test coverage reports |

### 4b. Trust the HTTPS dev certificate

The launch profile serves `https://localhost:8080`:

```bash
dotnet dev-certs https --trust
```

Linux may need extra steps — see the [official docs](https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl).

### 4c. Build

```bash
dotnet build
```

Warnings are errors; a clean build has none.

### 4d. Run

```bash
dotnet run --project src/Presentation.WebApi
```

API at `https://localhost:8080`; the OpenAPI document (no Swagger UI) at `https://localhost:8080/openapi/v1.json`, Development only.

> **Config:** `dotnet run` uses the Development environment, whose `appsettings.Development.json` supplies the connection string, the StreamElements base URL and **placeholder** `SMTP__*` values, so no environment variables are needed. Set real SMTP values to actually send mail. Full catalog: [CONFIGURATION.md](CONFIGURATION.md).

---

## 5. Frontend

```bash
cd frontend
npm install
cp .env.example .env   # gitignored
```

```env
VITE_API_URL=https://localhost:8080
```

Change `VITE_API_URL` if the backend runs elsewhere. Start the dev server:

```bash
npm run dev
```

Frontend at `http://localhost:5173`.

---

## 6. Initial app bootstrap

On first launch there is no user: `http://localhost:5173` redirects to `/bootstrap` to create the single account. Then add a credential for each provider you watch under **Settings → Store credentials** (StreamElements token, GauPoints `kp_session` cookie).

---

## 7. E2E tests (optional)

Install Playwright browsers once:

```bash
cd frontend
npx playwright install --with-deps
```

Then run the route-mocked suite (no backend, database or Docker):

```bash
npx playwright test --project=ui
```

`--project=ui` is required; the full-stack `real` suite and every other test command are in [BUILD-AND-TEST.md](BUILD-AND-TEST.md).

---

## 8. Running the full Docker stack (optional)

To run the production image stack locally:

```bash
docker compose up -d
```

| Service | Address |
|---|---|
| Web UI + API (Caddy TLS proxy → nginx → backend) | `https://localhost` (port `443`, the only published port) |
| Backend, PostgreSQL | Not published — compose network only; use `docker compose exec` |

To reach it from another device, set `PUBLIC_HOST` in `.env` to a name that resolves to this machine. Trust the proxy's root CA or accept the warning — see [DEPLOYMENT.md § HTTPS](DEPLOYMENT.md#https). `http://` is refused by design.

That pulls GHCR images. To run local changes, `scripts/verify-local.sh` with **no flags** publishes, builds both images under the names Compose resolves, checks they run as non-root, and runs the real E2E:

```bash
scripts/verify-local.sh
IMAGE_TAG=local docker compose up -d
```

> `--skip-real` stops before the build; `--skip-build` reuses existing `:local` images.

Or build by hand:

```bash
# Backend — publish into an arch subdir; the image does COPY ${TARGETARCH}/
# The publish path resolves from backend/, the build context from the repo root.
( cd backend && dotnet publish src/Presentation.WebApi/Presentation.WebApi.csproj \
  -c Release --self-contained true -r linux-musl-x64 \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true -o ./publish/amd64 )
docker build -f backend/Dockerfile \
  -t ghcr.io/luanholiv/pointlurker-backend:local backend/publish

# Frontend — the context is a staging dir holding just dist/ + nginx.conf, NOT frontend/
npm run build --prefix frontend
CTX="$(mktemp -d)" && mkdir -p "$CTX/dist"
cp -r frontend/dist/* "$CTX/dist/" && cp frontend/nginx.conf "$CTX/nginx.conf"
docker build -f frontend/Dockerfile \
  -t ghcr.io/luanholiv/pointlurker-frontend:local "$CTX"
```

Use exactly these image names: they are what `IMAGE_TAG=local` resolves to, and any other name leaves Compose pulling from GHCR. Why the context is never the Dockerfile's own directory: [CI-CD.md](CI-CD.md#build-context).

---

## 9. Automated dependency updates (repo admin)

Renovate (`renovate.json`) opens weekly update PRs for NuGet, npm, GitHub Actions, Dockerfile and Docker Compose images, plus the `custom.regex` pins, after a 7-day minimum release age. Base images travel as one "docker images" group, and the PostgreSQL major is held back. Action and tool pins and their reasoning: [CI-CD.md](CI-CD.md#action-pinning).

**One-time setup:** install the [Renovate GitHub App](https://github.com/apps/renovate) on the repository; it picks up `renovate.json` on its own.

- **Actions** are pinned by commit SHA; the `github-actions` manager moves them in one weekly `github actions` PR, never automerged, with majors held on the dependency dashboard.
- **Advisories** take a fast path: `osvVulnerabilityAlerts` checks dependencies against [OSV](https://osv.dev/), and those PRs skip the schedule and release-age wait, arrive ungrouped and labelled `security`, and are committed as `fix(deps): … [SECURITY]` so they cut a release.
- **Dependabot alerts are off**, so OSV is the only advisory feed (Snyk SCA in `security-scan` stays the independent gate). OSV has no GitHub Actions datasource, so an advisory against an action reaches no feed. If Snyk flags something Renovate raised no `[SECURITY]` PR for, or an action advisory appears, enable **Settings → Advanced Security → Dependency graph + Dependabot alerts**, and give the Renovate app `read` access to Dependabot alerts.

---

## 10. Workflow token permissions (repo admin)

**One-time setup:** **Settings → Actions → General → Workflow permissions → Read repository contents and packages permissions**. Audit it with:

```bash
gh api repos/:owner/:repo/actions/permissions/workflow
# {"default_workflow_permissions":"read","can_approve_pull_request_reviews":false}
```

This default is only a backstop: CI cannot read it, so every workflow declares its own scopes in YAML. Which jobs need what, and why: [CI-CD.md](CI-CD.md#token-permissions).

---

## 11. Verify the setup

| Check | Command | Expected |
|---|---|---|
| Backend build clean | `dotnet build` (from `backend/`) | No errors, no warnings |
| Unit + integration tests | `dotnet test --filter "Category!=LiveProbe&Category!=LiveProbeWrite"` (from `backend/`) | All pass; needs Docker. The filter excludes both live probes. |
| Frontend build clean | `npm run build` (from `frontend/`) | No errors |
| Frontend unit tests | `npm run test` (from `frontend/`) | All pass |
| Frontend type check | `npm run typecheck` (from `frontend/`) | No errors |
| Backend formatter check | `dotnet csharpier check .` (from `backend/`) | No diff |
| Frontend formatter check | `npm run format:check` (from `frontend/`) | No diff |
| Git hooks active | `git config core.hooksPath` | Ends in `.husky/_` |
