import { test, expect, type Page } from "@playwright/test";
import { TOKEN_KEY, forbidPageRoute, loginAsAdmin } from "./realStack";

// Real, UNMOCKED end-to-end coverage of the app-session token refresh flows against the full
// Docker Compose stack (only StreamElements faked). Companion to smoke.real.spec.ts.
//
// The refresh scenarios are made DETERMINISTIC (no 60s waits) by manipulating the persisted
// token set in localStorage — the same storage the app reads — instead of waiting for a real
// clock expiry. The storage key + shape mirror frontend/src/features/auth/authStorage.ts:
//   localStorage["pl.auth.token"] = { accessToken, refreshToken, expiresAt }
//
// Re-entrant by design (one retry, no DB reset between attempts): registration happens only on a
// fresh stack; otherwise we log in with the same deterministic credentials.

test.beforeEach(async ({ page }) => {
  test.setTimeout(120_000);
  // A real-smoke spec must never intercept /api — it must hit the real backend.
  forbidPageRoute(page, "auth-refresh.real.spec.ts");
});

const readTokenSet = (page: Page) =>
  page.evaluate((key) => {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as Record<string, unknown>) : null;
  }, TOKEN_KEY);

const writeTokenSet = (page: Page, patch: Record<string, unknown>) =>
  page.evaluate(
    ([key, p]) => {
      const raw = localStorage.getItem(key as string);
      const current = raw ? JSON.parse(raw) : {};
      localStorage.setItem(
        key as string,
        JSON.stringify({ ...current, ...(p as object) }),
      );
    },
    [TOKEN_KEY, patch] as const,
  );

test("stale access token is transparently refreshed on the next request (reactive)", async ({
  page,
}) => {
  await loginAsAdmin(page);

  // Corrupt ONLY the access token; keep the (valid) refresh token. Keep expiresAt in the future
  // so the PROACTIVE path stays quiet — this isolates the reactive 401 -> refresh -> retry path.
  await writeTokenSet(page, {
    accessToken: "stale-and-invalid",
    expiresAt: Date.now() + 3_600_000,
  });

  // Reload lands in the app and issues authenticated requests (GET /api/users/me, /api/stores).
  // The stale token 401s, the client refreshes once with the valid refresh token and retries.
  await page.goto("/stores");

  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toBeVisible();
  await expect(page.getByRole("heading", { name: "Sign in" })).toHaveCount(0);

  // A fresh access token was persisted by the refresh.
  const after = await readTokenSet(page);
  expect(after?.accessToken).not.toBe("stale-and-invalid");
});

test("near-expiry token is proactively refreshed when the tab regains visibility", async ({
  page,
}) => {
  await loginAsAdmin(page);

  // Mark the token as at-expiry so the proactive path should fire.
  await writeTokenSet(page, { expiresAt: Date.now() });

  const refreshDone = page.waitForResponse(
    (r) => r.url().endsWith("/api/auth/refresh") && r.ok(),
    { timeout: 30_000 },
  );

  // Simulate the tab regaining visibility.
  await page.evaluate(() => {
    Object.defineProperty(document, "visibilityState", {
      configurable: true,
      get: () => "visible",
    });
    document.dispatchEvent(new Event("visibilitychange"));
  });

  await refreshDone; // proactive refresh issued
  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toBeVisible();
});

test("expired refresh token forces a real logout and clears storage", async ({
  page,
}) => {
  await loginAsAdmin(page);

  // Corrupt BOTH tokens: the access token 401s and the refresh token cannot be exchanged.
  await writeTokenSet(page, {
    accessToken: "stale-and-invalid",
    refreshToken: "expired-and-invalid",
    expiresAt: Date.now() - 1000,
  });

  await page.goto("/stores");

  // The failed refresh clears storage and surfaces a real logout (back to the sign-in screen).
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
  const after = await readTokenSet(page);
  expect(after).toBeNull();
});

// NOTE: the "second registration is rejected" scenario is NOT an E2E test — once a user exists
// the bootstrap page is guarded (the app routes to /login), so a second registration is not
// reachable through the UI. It is covered by backend integration tests
// (BootstrapRegistrationTests + SingleUserValidatorIntegrationTests).
