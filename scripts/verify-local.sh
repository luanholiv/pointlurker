#!/usr/bin/env bash
#
# verify-local.sh — full local verification that mirrors CI.
#
# Runs every test except the live probe, building FRESH backend + frontend Docker
# images from local source so the real E2E smoke runs against your code (not a
# stale published image). See docs/BUILD-AND-TEST.md for the full description.
#
# Usage:
#   scripts/verify-local.sh                # everything (fresh images + real E2E)
#   scripts/verify-local.sh --skip-build   # reuse existing :local images for the real E2E
#   scripts/verify-local.sh --skip-real    # backend + frontend + UI tests only
#   scripts/verify-local.sh --help
#
# Requires: .NET 10 SDK, Node 22, Docker (running). First Playwright run may need
#           `npx playwright install --with-deps`.
#
# The real E2E runs in an ISOLATED Compose project ("pl-e2e") that publishes only
# the frontend on ${E2E_FRONTEND_PORT:-8088} and Mailpit's web API on
# ${E2E_MAILPIT_PORT:-8026} — never 80/8080/5432/8025. So it coexists
# with a running dev stack and never touches the dev postgres_data volume; this
# project is wiped (down -v) before and after the run, so the schema always applies
# to a fresh, empty database.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

IMAGE_TAG="local"
BACKEND_IMAGE="ghcr.io/luanholiv/pointlurker-backend:${IMAGE_TAG}"
FRONTEND_IMAGE="ghcr.io/luanholiv/pointlurker-frontend:${IMAGE_TAG}"

# Isolated, throwaway Compose project — does NOT share the dev project nor its
# postgres_data volume. The e2e stack publishes only the frontend port below, so it
# runs alongside a dev `docker compose up` without a port clash.
E2E_PROJECT="pl-e2e"
E2E_FRONTEND_PORT="${E2E_FRONTEND_PORT:-8088}"
# The availability smoke reads the captured mail over this port; overriding it has to move the
# container mapping and the spec's URL together, which is why both are derived from here.
E2E_MAILPIT_PORT="${E2E_MAILPIT_PORT:-8026}"
COMPOSE=(docker compose -p "$E2E_PROJECT" -f docker-compose.yml -f docker-compose.e2e.yml)

SKIP_BUILD=0
SKIP_REAL=0
for arg in "$@"; do
  case "$arg" in
    --skip-build) SKIP_BUILD=1 ;;
    --skip-real)  SKIP_REAL=1 ;;
    -h|--help)
      # Print the leading comment block (skip the shebang; stop at first code line).
      awk 'NR>1 && /^#/{sub(/^# ?/,""); print; next} NR>1{exit}' "${BASH_SOURCE[0]}"
      exit 0 ;;
    *) echo "Unknown option: $arg (try --help)" >&2; exit 2 ;;
  esac
done

# Map host arch -> .NET RID + Docker TARGETARCH (backend/Dockerfile copies ${TARGETARCH}/).
case "$(uname -m)" in
  x86_64|amd64)        RID="linux-musl-x64";   TARGETARCH="amd64" ;;
  aarch64|arm64)       RID="linux-musl-arm64"; TARGETARCH="arm64" ;;
  *) echo "Unsupported arch: $(uname -m)" >&2; exit 1 ;;
esac

step() { printf '\n\033[1;36m==> %s\033[0m\n' "$*"; }

# Both runtime images must drop root. Nothing else in the repo asserts this: a missing
# USER is invisible to every test we run, and hadolint does not catch it either — its only
# user rule fires on an explicit trailing `USER root`, and its numeric-uid rule only fires
# on a USER that is already there. Reading it back off the built image also covers a
# base-image change quietly reintroducing root.
assert_nonroot() {
  local image="$1" user
  user="$(docker image inspect -f '{{.Config.User}}' "$image")"
  case "$user" in
    "" | root | 0 | 0:*)
      printf '\n\033[1;31mFAIL: %s runs as root (Config.User=%s)\033[0m\n' \
        "$image" "${user:-<empty>}" >&2
      exit 1
      ;;
  esac
  printf '    %s runs as %s\n' "$image" "$user"
}

# The outbound emails build a named culture to choose their translation. Alpine carries no ICU, so
# the .NET base image declares globalization-invariant mode, under which that throws — and it
# throws nowhere a request can see, because mail is sent from a background cycle. The real smoke
# does catch it, a few minutes later and as a timeout waiting for a message; this says why.
assert_globalization() {
  local image="$1" invariant
  invariant="$(docker image inspect -f \
    '{{range .Config.Env}}{{if eq . "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true"}}{{.}}{{end}}{{end}}' \
    "$image")"
  if [ -n "$invariant" ]; then
    printf '\n\033[1;31mFAIL: %s runs in globalization-invariant mode; localized email will throw\033[0m\n' \
      "$image" >&2
    exit 1
  fi
  printf '    %s has cultures available\n' "$image"
}

STACK_UP=0
cleanup() {
  if [ "$STACK_UP" = "1" ]; then
    step "Tearing down E2E stack"
    IMAGE_TAG="$IMAGE_TAG" "${COMPOSE[@]}" down -v || true
  fi
}
trap cleanup EXIT

# ---------------------------------------------------------------------------
# 1. Backend tests (excludes live probe; integration tests need Docker)
# ---------------------------------------------------------------------------
step "Backend tests (unit + architecture + contract + integration)"
( cd backend && dotnet test --filter "Category!=LiveProbe&Category!=LiveProbeWrite" )

# ---------------------------------------------------------------------------
# 2. Frontend unit/component tests
# ---------------------------------------------------------------------------
step "Frontend unit tests"
( cd frontend && { [ -d node_modules ] || npm ci; } && npm run test )

# ---------------------------------------------------------------------------
# 3. UI tests (mocked API, no backend)
# ---------------------------------------------------------------------------
step "UI tests (Playwright --project=ui, mocked API, no backend)"
( cd frontend && npx playwright install >/dev/null 2>&1 || true; npx playwright test --project=ui )

if [ "$SKIP_REAL" = "1" ]; then
  step "Skipping real E2E (--skip-real). Backend, frontend, and UI tests passed."
  exit 0
fi

# ---------------------------------------------------------------------------
# 4. Build fresh :local images from local source
# ---------------------------------------------------------------------------
if [ "$SKIP_BUILD" = "1" ]; then
  step "Reusing existing ${IMAGE_TAG} images (--skip-build)"
else
  step "Building backend image (${BACKEND_IMAGE})"
  ( cd backend && dotnet publish src/Presentation.WebApi/Presentation.WebApi.csproj \
      -c Release \
      --self-contained true \
      -r "$RID" \
      -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:EnableCompressionInSingleFile=true \
      -o "./publish/${TARGETARCH}" )
  docker build -f backend/Dockerfile -t "$BACKEND_IMAGE" backend/publish

  step "Building frontend image (${FRONTEND_IMAGE})"
  # No VITE_API_URL: the bundle falls back to page origin and reaches the backend
  # through the nginx /api proxy baked into frontend/nginx.conf (production image).
  ( cd frontend && { [ -d node_modules ] || npm ci; } && npm run build )
  # The Dockerfile lives in frontend/, but frontend/ is NOT the build context: -f and
  # the context are independent. The image wants exactly dist/ + nginx.conf at the
  # context root, and there is no .dockerignore under frontend/ to trim anything else,
  # so building from there would ship node_modules and the whole source tree to the
  # daemon. Assemble a minimal context instead.
  FE_CTX="$(mktemp -d)"
  mkdir -p "$FE_CTX/dist"
  cp -r frontend/dist/* "$FE_CTX/dist/"
  cp frontend/nginx.conf "$FE_CTX/nginx.conf"
  docker build -f frontend/Dockerfile -t "$FRONTEND_IMAGE" "$FE_CTX"
  rm -rf "$FE_CTX"

  step "Asserting both images run as a non-root user"
  assert_nonroot "$BACKEND_IMAGE"
  assert_nonroot "$FRONTEND_IMAGE"
  assert_globalization "$BACKEND_IMAGE"
fi

# ---------------------------------------------------------------------------
# 5. E2E real smoke against the full stack + WireMock SE
# ---------------------------------------------------------------------------
step "Resetting any leftover e2e stack (fresh database)"
# Remove a previous (possibly crashed) e2e project so the DB starts empty. Only ever
# touches the isolated "$E2E_PROJECT" project — never the dev stack's volume.
IMAGE_TAG="$IMAGE_TAG" "${COMPOSE[@]}" down -v --remove-orphans >/dev/null 2>&1 || true

step "Starting E2E stack (project=${E2E_PROJECT}, frontend on :${E2E_FRONTEND_PORT}, mailpit on :${E2E_MAILPIT_PORT})"
STACK_UP=1   # set before `up` so a partial/failed start is still torn down
IMAGE_TAG="$IMAGE_TAG" E2E_FRONTEND_PORT="$E2E_FRONTEND_PORT" E2E_MAILPIT_PORT="$E2E_MAILPIT_PORT" \
  "${COMPOSE[@]}" up -d --wait --wait-timeout 180

step "E2E real smoke (Playwright --project=real)"
( cd frontend && CI=true CI_BASE_URL="http://localhost:${E2E_FRONTEND_PORT}" \
    E2E_MAILPIT_URL="http://localhost:${E2E_MAILPIT_PORT}" \
    npx playwright test --project=real --workers=1 )

step "All tests passed ✅ (live probe excluded by design)"
