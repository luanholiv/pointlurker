# PointLurker

**English** · [Português (Brasil)](README.pt-BR.md)

[![Release](https://img.shields.io/github/v/release/luanholiv/pointlurker?sort=semver&logo=github)](https://github.com/luanholiv/pointlurker/releases/latest)
[![Backend image](https://img.shields.io/badge/GHCR-backend-2496ED?logo=docker&logoColor=white)](https://github.com/luanholiv/pointlurker/pkgs/container/pointlurker-backend)
[![Frontend image](https://img.shields.io/badge/GHCR-frontend-2496ED?logo=docker&logoColor=white)](https://github.com/luanholiv/pointlurker/pkgs/container/pointlurker-frontend)
[![License](https://img.shields.io/github/license/luanholiv/pointlurker)](LICENSE)

[![CI](https://img.shields.io/github/actions/workflow/status/luanholiv/pointlurker/ci.yml?branch=main&logo=github&label=CI)](https://github.com/luanholiv/pointlurker/actions/workflows/ci.yml)
[![Store API Probe](https://img.shields.io/github/actions/workflow/status/luanholiv/pointlurker/store-api-live-probe.yml?logo=github&label=store%20API%20probe)](https://github.com/luanholiv/pointlurker/actions/workflows/store-api-live-probe.yml)
[![codecov](https://img.shields.io/codecov/c/github/luanholiv/pointlurker?logo=codecov)](https://codecov.io/gh/luanholiv/pointlurker)
[![Known Vulnerabilities](https://snyk.io/test/github/luanholiv/pointlurker/badge.svg)](https://snyk.io/test/github/luanholiv/pointlurker)

A self-hosted web app that watches loyalty-point stores — [StreamElements](https://streamelements.com/) channel stores and [GauPoints](https://gaupoints.com.br) — emails you when a wishlisted item becomes available, and can redeem it for you automatically.

## Features

- **Two store providers**: any number of StreamElements stores, plus the GauPoints store
- **Wishlist**: save items, with per-item email alerts and optional auto-redeem
- **Automatic redemption**: redeems when an item is available, affordable and every required input is filled in
- **Email notifications**: item availability, gift-card codes returned by a redemption, and a warning before a store login expires
- **Secure single-user account**: password plus optional TOTP two-factor; store credentials encrypted at rest
- **Bilingual**: English and Português (BR), including notification emails
- **Light/dark theme**

## Quick Start

Run the full stack (TLS proxy, frontend, backend, PostgreSQL) with Docker Compose:

```bash
cp .env.example .env    # then set the required SMTP__* values
docker compose up -d
```

The app is served **over HTTPS only** at `https://localhost` (set `PUBLIC_HOST` to reach it from other devices), using a certificate from the proxy's own local CA; trust its root once per device — [docs/DEPLOYMENT.md § HTTPS](docs/DEPLOYMENT.md#https). The first visit walks you through creating your account.

The backend refuses to start without `SMTP__HOST`, `SMTP__USERNAME`, `SMTP__PASSWORD` and `SMTP__FROM`. All variables: [docs/CONFIGURATION.md](docs/CONFIGURATION.md). Self-hosting, Gmail setup and backups: [docs/DEPLOYMENT.md](docs/DEPLOYMENT.md).

## Development

Prerequisites: the .NET SDK pinned in [`backend/global.json`](backend/global.json), Node.js 22+, and Docker (for PostgreSQL and the integration tests).

```bash
npm install                                    # repo root: git hooks + dotnet tools
docker compose up database -d                  # PostgreSQL only

cd backend && dotnet run --project src/Presentation.WebApi    # terminal 1: https://localhost:8080

cd frontend && npm install && cp .env.example .env && npm run dev   # terminal 2: http://localhost:5173
```

Tests:

```bash
# from backend/ — excludes the live probes, which call the real provider APIs
dotnet test --filter "Category!=LiveProbe&Category!=LiveProbeWrite"

# from frontend/
npm run test
```

Full setup: [docs/SETUP.md](docs/SETUP.md). Every build and test command: [docs/BUILD-AND-TEST.md](docs/BUILD-AND-TEST.md). How it works: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md).

## License

This project is released into the public domain under [the Unlicense](LICENSE).
