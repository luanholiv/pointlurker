import { test, expect } from "@playwright/test";
import {
  EMAIL,
  dismissMissingLabelPrompt,
  ensureRegistered,
  ensureSeToken,
  forbidPageRoute,
  loginAsAdmin,
  storeIsListed,
} from "./realStack";

// Real, UNMOCKED end-to-end smoke. Drives the production frontend bundle against a
// real backend + real PostgreSQL; only StreamElements is faked (WireMock sidecar).
// Run with: npx playwright test --project=real  (against the docker-compose.e2e stack).
//
// Re-entrant by design: the `real` project runs with one retry and NO database reset
// between attempts, so every step is idempotent. Registration happens only when the
// stack has no user yet; otherwise we log in with the same deterministic credentials.
// The terminal assertions are steady states that hold on both the fresh attempt and a
// retry against the already-seeded stack.

const WRONG_PASSWORD = "WrongPass123!";
const SE_USERNAME = "e2e_streamer";
// Must match frontend/tests/e2e/wiremock/__files/channel-response.json displayName.
const STORE_DISPLAY_NAME = "E2E Test Store";
// Must match the 404 WireMock stub at mappings/channels-lookup-notfound.json.
const UNKNOWN_USERNAME = "ghost_streamer";

test.beforeEach(async ({ page }) => {
  test.setTimeout(120_000);

  // Guard (spec scenario: "no page.route handler"): a real-smoke spec must never
  // intercept /api — it must hit the real backend.
  forbidPageRoute(page, "smoke.real.spec.ts");
});

test("login then add store succeeds end to end", async ({ page }) => {
  // 1-2. Bootstrap-aware register (fresh) + login (no 2FA for a fresh user).
  await loginAsAdmin(page);

  // 3. Ensure the StreamElements token is set (gates POST /api/stores). Idempotent.
  await ensureSeToken(page);

  // 4. Add the store via the real UI: lookup channel -> create. Skip if already present.
  if (!(await storeIsListed(page, STORE_DISPLAY_NAME))) {
    await page.getByRole("button", { name: "Add Store" }).first().click();
    await page.getByPlaceholder("e.g. xqc").fill(SE_USERNAME);
    await page.getByRole("button", { name: "Lookup" }).click();
    // Channel resolved from the WireMock stub. Scoped to the dialog: the same name will
    // appear behind it once the store exists.
    await expect(
      page.getByRole("dialog").getByText(STORE_DISPLAY_NAME),
    ).toBeVisible();
    await page.getByRole("button", { name: /Continue/i }).click();
    // Every fixture label starts unresolved, so the missing-label prompt always opens.
    await dismissMissingLabelPrompt(page);
  }

  // 5. Terminal assertion: the store card is visible on the Stores page. Scoped to <main>,
  // so it cannot be satisfied by the Add-store dialog's echo of the same name.
  await expect(
    page.getByRole("main").getByText(STORE_DISPLAY_NAME).first(),
  ).toBeVisible();
});

test("channel lookup miss renders the real ProblemDetails error", async ({
  page,
}) => {
  // Proves the WHOLE problem+json contract through the real stack: the backend emits a
  // `store_lookup.not_found` ProblemDetails with a `query` param, apiClient parses it into
  // a typed ApiError, and the UI resolves it via the frontend error catalog (tError) WITH
  // parameter interpolation. Read-only — no DB mutation, so it is retry-safe.
  await loginAsAdmin(page);

  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();
  await page.getByPlaceholder("e.g. xqc").fill(UNKNOWN_USERNAME);
  await page.getByRole("button", { name: "Lookup" }).click();

  // WireMock returns 404 for this username -> ProviderApiException(404) -> store_lookup.not_found.
  await expect(page.locator("#lookup-error")).toHaveText(
    `No store found for '${UNKNOWN_USERNAME}'.`,
  );
});

test("login with a wrong password is rejected by the real backend", async ({
  page,
}) => {
  // Negative auth proof the happy path lacks: the real backend rejects bad credentials.
  // Read-only — no DB mutation, so it is retry-safe.
  await ensureRegistered(page);

  await page.getByLabel("Email").fill(EMAIL);
  await page.getByLabel("Password", { exact: true }).fill(WRONG_PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();

  // Real 401 surfaced in the UI as catalog copy, not as the transport detail: the backend sends a
  // code and the page renders its own sentence for it. The authenticated app is never reached.
  await expect(page.getByText("Invalid credentials.")).toBeVisible();
  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toHaveCount(0);
});
