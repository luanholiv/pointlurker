import { defineConfig } from "@playwright/test";

const ciBaseUrl = process.env.CI_BASE_URL;

export default defineConfig({
  reporter: [["html"], ["junit", { outputFile: "test-results/junit.xml" }]],
  use: {
    locale: "en-US",
    // Opt-in: use a system Chromium when the bundled browser can't be installed
    // (e.g. restricted CI/sandbox). No effect unless E2E_CHROMIUM_PATH is set.
    launchOptions: process.env.E2E_CHROMIUM_PATH
      ? { executablePath: process.env.E2E_CHROMIUM_PATH }
      : undefined,
  },
  projects: [
    {
      // Default, fast: route-mocked UI tests. Real browser + real frontend bundle,
      // but every /api call is stubbed with page.route — no backend/DB/Docker.
      // Not end-to-end: the frontend tested as an integrated unit in isolation.
      name: "ui",
      testDir: "./tests/ui",
      testMatch: "**/*.spec.ts",
      use: { baseURL: ciBaseUrl || "http://127.0.0.1:5173" },
    },
    {
      // Opt-in: the one real end-to-end smoke — real browser bundle against the full
      // Docker Compose stack (only StreamElements faked). Single worker + one retry;
      // the specs are re-entrant so the retry survives the already-seeded stack
      // (see tests/e2e/smoke.real.spec.ts). `workers` is a global option (not
      // per-project) — CI runs this project with `--workers=1`, which the specs rely
      // on: they share one stack and one database, with no reset between them.
      name: "real",
      testDir: "./tests/e2e",
      testMatch: "**/*.real.spec.ts",
      retries: 1,
      // The e2e compose stack publishes the frontend on E2E_FRONTEND_PORT (default
      // 8088) and Mailpit's web API on E2E_MAILPIT_PORT (default 8026) — never 80 or
      // 8025 — so it coexists with a running dev stack.
      use: { baseURL: ciBaseUrl || "http://localhost:8088" },
    },
  ],
  // Local convenience only: serve the vite dev server for the `mocked` project.
  // Disabled under CI (CI serves a built bundle / the stack); the `real` project
  // always targets CI_BASE_URL and ignores this.
  webServer: process.env.CI
    ? undefined
    : {
        command: "npm run dev -- --host",
        url: "http://127.0.0.1:5173",
        reuseExistingServer: true,
      },
});
