# CI/CD

Why the pipeline is shaped the way it is. The rules are summarised in `AGENTS.md`;
this is the reasoning behind each one, needed only when editing
`.github/workflows/**`.

What the published images are and how to verify them: [DEPLOYMENT.md](DEPLOYMENT.md).
Running the same checks locally: [BUILD-AND-TEST.md](BUILD-AND-TEST.md).

---

## Workflow files

**Three files, split by entry point:** `ci.yml` (push/PR to `main`), `cd.yml`
(called by `ci.yml`, plus its own `workflow_dispatch`), `store-api-live-probe.yml`
(weekly cron, one step per provider).

Nothing else is a reusable workflow. A `workflow_call` file invoked from one place
buys nothing and costs `secrets: inherit`, a caller/callee permission coupling, and
the inability of one of its jobs to declare a narrower `needs:` than its caller
passed down. A *file* boundary marks an entry point; a *job* boundary marks a
concern or a permission change.

**`cd.yml` must never be renamed.** Keyless cosign bakes the workflow path into the
certificate identity, and [DEPLOYMENT.md](DEPLOYMENT.md) publishes that path in the
verification command users run.

---

## Job structure

### CD is three jobs: `build` → `security-scan` → `publish`

Only `publish` holds `id-token: write`, so it is the one job that can mint a
Sigstore signing identity. `security-scan` is separate so a Snyk fault or quota 403
can be retried without rebuilding and re-pushing every image.

A **skipped** `build` must not skip `security-scan`: that is the `workflow_dispatch`
path, which re-drives a release from registry tags alone, so its `if:` tests
`needs.build.result` explicitly. The job id is `security-scan`, matching `ci.yml`'s —
an edit written against a bare `scan` targets a job that does not exist.

### Snyk runs in one `security-scan` job, never inside the jobs it gates

Keeping it out of `backend`/`frontend` stops a paid, quota-limited service from
delaying or failing the test run, the coverage upload and the
`backend-publish`/`frontend-dist` artifacts that `e2e-tests` and `cd` download. Do
not move the steps back into a producing job.

One job for both stacks: they share the tool, the token and the account quota, so
one job means one Snyk setup and one red check, and per-job billing rounds up to the
minute. It produces nothing and gates everything — the shape `gitleaks` and
`hadolint` also use.

Every `if:` inside it is **step level, never job level**: `release` needs this job,
and a skipped job skips its dependents, so a job-level guard would silently skip
releases on single-stack changes.

The backend SCA needs no `--exclude=publish` (`publish/` is produced in another
job). The frontend SCA runs from the repository root with `--exclude=backend`, which
also covers the root `package-lock.json`.

### `security-scan` prunes shared runtimes with no targeting pack

`actions/setup-dotnet` always installs the newest LTS *runtime* before the pinned
SDK, with no input to disable it. A runtime-only install ships no targeting pack, so
on a .NET patch day there is a `Microsoft.NETCore.App` version with no
`packs/Microsoft.NETCore.App.Ref/<version>` beside it. Snyk's .NET plugin reads
`PackageOverrides.txt` from the pack matching the newest runtime, so the gap aborts
the scan with `SNYK-CLI-0000`.

The prune runs right after `Setup .NET` in `security-scan`, not in `backend`, because
only Snyk notices the extra runtime. Bumping `snyk-version` or pinning
`backend/global.json` does not help: every plugin version builds the path the same
way, and `setup-dotnet` ignores `rollForward`.

---

## Test jobs

### `ui-tests` runs in the Playwright image, and the tag must match the lockfile

`mcr.microsoft.com/playwright:v<version>-noble` ships the browsers and their system
libraries, replacing an `apt-get` step measured between 28s and 821s. The tag must
match `@playwright/test` in `frontend/package-lock.json`, which is pinned exactly (no
caret) so a Renovate bump is one visible change; a guard step fails in seconds on
drift.

**`e2e-tests` cannot use that image.** It drives `docker` and `docker compose`, which
a job container has no client or socket for, and its specs read two host-published
ports (frontend `8088`, Mailpit `8026`) that a container's own network namespace
would not see.

---

## Images

### Build context

`backend/Dockerfile` / `frontend/Dockerfile` are runtime-only. Each expects a
pre-built artifact as build context — backend: `backend-publish` with
`amd64/`+`arm64/` subdirs; frontend: `dist/` + `nginx.conf`.

**A Dockerfile is never built with its own directory as context.** `-f` and the
context are independent, so the context is a staging dir of build output
(`docker build -f backend/Dockerfile … backend/publish`, never `… backend/`). There
is no dev backend Dockerfile and no Aspire — local dev is plain `dotnet run` (see
[SETUP.md](SETUP.md)).

### The frontend is built origin-relative

No `VITE_API_URL` at CD: nginx reverse-proxies `/api`, so the image is portable and
`VITE_API_URL` is dev-server-only. CD ships the `frontend-dist` artifact E2E tested
rather than rebuilding it, so no third-party install runs inside the deployment
workflow.

### `:latest` is created only after signing, never by a build

Builds push `:v{version}` only. `cd.yml`'s last registry step copies `:latest` onto
the digest just signed and attested, then asserts it resolved to that digest. Do not
add `-t …:latest` to a build or the manifest merge: a tag pushed before the Snyk gate
is one a failed scan leaves pointing at an unsigned image, and `:latest` is what a
self-hoster runs by default (`IMAGE_TAG` defaults to `latest`).

The arch-suffixed `:v{version}-amd64`/`-arm64` tags are unsigned intermediates;
`cosign sign` runs against the manifest-list digest only.

### Non-root images

`e2e-tests` reads the user back off both built images and fails if either runs as
root. No lint catches this — hadolint flags only an explicit trailing `USER root` —
and reading the image also catches a base-image change reintroducing root. It lives
in `e2e-tests` because that job builds both images; the two Dockerfiles sit in
different path filters.

### Dockerfile linting

**`hadolint` is a top-level job in `ci.yml` that always runs, and `release` needs
it.** No `if:` and no path filter: the two Dockerfiles sit in different path
filters, so a job behind either would be blind to single-stack PRs. The `release`
edge is the enforcement — a failing job stops only the jobs that need it.

`hadolint/hadolint-action` is pinned by commit SHA, and that is the whole pin: it is
a docker action whose own Dockerfile pins `ghcr.io/hadolint/hadolint` by digest.

**No `.hadolint.yaml` and no inline `# hadolint ignore`** — findings are fixed at the
source. The default failure threshold is `info`, so rules such as `DL3066`
(non-numeric user id — hence `USER 1000`/`USER 101`) fail the build too.

Neither runtime image installs `curl`: the compose healthchecks use the BusyBox
`wget` applet both bases ship, whose failure semantics match `curl -f`.

---

## Secret scanning

**`gitleaks` is a top-level job in `ci.yml` that always runs, and `release` needs
it.** No `if:` and no path filter, because a secret can land in any file. The Snyk
placement rule does not apply: gitleaks is a deterministic local binary with no
account, no quota and no artifacts.

The `release` edge is the whole enforcement: required status checks are not
available on this repository's plan, and without the edge a red scan would fail the
workflow while `release` and `cd` published images from the offending commit.

It runs the `git` subcommand (not the deprecated `detect`) over full history with
`fetch-depth: 0`; a shallow clone would scan only the tip. `gitleaks/gitleaks-action`
is not used: it resolves its binary to latest unless pinned, needs a `GITHUB_TOKEN`,
and defaults PR commenting on, which wants `pull-requests: write`.

**`.gitleaks.toml` carries no allowlist, and an exemption is only ever scoped to commit
SHAs — never to a path or a literal.** If a benign secret-shaped string reaches history,
allowlist the commits that carry it. A commit allowlist only matches history, so
reintroducing the string still fails; a path or regex exemption would permit it forever. No
`.gitleaksignore` and no `--baseline-path` either: both suppress by opaque
fingerprint and absorb new findings whenever regenerated.

The default `generic-api-key` stopword list is what keeps `.env.example` placeholders
and token-shaped fixtures clean — a stopword matches as a substring of the detected
value, so `not-a-real-token` passes and a bare `.test` suffix does not.

---

## Action pinning

**Every third-party action is pinned by commit SHA with the tag as a trailing
comment** (`uses: actions/checkout@11d5960a… # v4`); a branch ref is never
acceptable. Renovate maintains the SHAs (`github-actions` manager, grouped as
`github actions`); majors wait on the dependency dashboard.

The Snyk step also pins `snyk-version`, because the action interpolates it into a
download URL and defaults it to `latest` — a pinned ref would otherwise still run
whatever binary is current. The leading `v` is part of the URL path.

---

## Token permissions

**Every workflow declares its `GITHUB_TOKEN` scopes.** The read-only repository
default is only a backstop: it is not in version control, invisible in review, lost
on a fork, and cannot be asserted from CI (reading it needs Administration). A
`permissions:` block is a whitelist — every scope left unnamed becomes `none`.

Only two jobs add a scope other than `contents`:

- `path-filter` — `pull-requests: read`. `dorny/paths-filter` lists changed files
  through the API with no fallback, so dropping it skips `backend`, `frontend`,
  `security-scan` and everything downstream.
- `cd` — `packages: write` (`build` and `publish` push to GHCR with the workflow
  token) and `id-token: write` (signing).

`release` holds `contents: write` alone, which depends on `.releaserc.json` keeping
`successComment`/`failComment`/`releasedLabels` all `false`.

A callee cannot ask for more than its caller granted — it fails validation — so
`ci.yml`'s `cd` job must keep granting exactly what `cd.yml`'s `publish` declares:
`contents: write`, `id-token: write`, `packages: write`.

A new workflow inherits read-only and declares anything more; for example, an
`actions/download-artifact` with a `run-id` for a cross-run fetch needs
`actions: read`. See [SETUP.md](SETUP.md).

---

## Releases

`release` skips CD if no commit is releasable (e.g. `chore`-only). CD's only secret is
`SNYK_TOKEN`; images go to `ghcr.io/luanholiv/...` with the workflow's own
`GITHUB_TOKEN`.

**A dependency bump only reaches the images if its commit is releasable.** Renovate
raises advisory-driven PRs as `fix(deps): … [SECURITY]` (`vulnerabilityAlerts`, fed
by `osvVulnerabilityAlerts`), ungrouped and outside the weekly schedule, and the
default release rules ship those. `.releaserc.json` adds one backstop rule promoting
a `chore(deps)` subject carrying a literal `[SECURITY]` to a patch; the brackets are
escaped because `commit-analyzer` matches with micromatch, where `[SECURITY]` is a
character class that matches almost any subject.
