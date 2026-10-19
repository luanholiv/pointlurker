import { expect, type Page } from "@playwright/test";

// Shared preamble for the `real` project: the bootstrap-aware register/login dance and the
// StreamElements-token gate, both of which every real spec needs before it can do anything.
//
// Extracted at the third copy. Every helper here is idempotent, because the `real` project
// runs with one retry and NO database reset between attempts: registration happens only when
// the stack has no user yet, and the token is written only when it is not already configured.

export const EMAIL = "admin@e2e.test";
export const PASSWORD = "Password123!";
export const TOKEN_KEY = "pl.auth.token";

/**
 * Trips on any `page.route` call. A real spec must reach the real backend; a stray route
 * handler would silently turn it into a mocked test that proves nothing about the stack.
 */
export function forbidPageRoute(page: Page, specName: string): void {
  page.route = (() => {
    throw new Error(
      `${specName} must NOT register page.route — it exercises the real backend`,
    );
  }) as typeof page.route;
}

/**
 * Lands on the login page with the single admin guaranteed to exist. Registers only on a
 * fresh stack (bootstrap shows "Create admin"); on an already-seeded stack the app routes
 * straight to the sign-in form, so registration is skipped.
 */
export async function ensureRegistered(page: Page): Promise<void> {
  await page.goto("/");

  const createAdmin = page.getByRole("button", { name: "Create admin" });
  await expect(
    createAdmin.or(page.getByRole("heading", { name: "Sign in" })),
  ).toBeVisible();

  if (await createAdmin.isVisible()) {
    await page.getByLabel("Email").fill(EMAIL);
    await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
    await page.getByLabel("Confirm new password").fill(PASSWORD);
    await createAdmin.click();
  }

  // Either path ends on the sign-in form.
  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
}

/**
 * Registers if needed, then logs in and lands in the authenticated app. A fresh user has 2FA
 * disabled, so there is no two-factor prompt.
 */
export async function loginAsAdmin(page: Page): Promise<void> {
  await ensureRegistered(page);

  await page.getByLabel("Email").fill(EMAIL);
  await page.getByLabel("Password", { exact: true }).fill(PASSWORD);
  await page.getByRole("button", { name: "Sign in" }).click();

  // A fresh stack lands on the setup checklist and one a previous spec set up on the stores, so
  // callers are handed the stores page either way.
  await expect(
    page.getByRole("heading", { level: 1, name: /^(Stores|Get started)$/ }),
  ).toBeVisible();
  await page.goto("/stores");
  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toBeVisible();
  await expect(page.getByLabel("Authenticator code")).toHaveCount(0);
}

/** Ensures the StreamElements token is set — it gates `POST /api/stores`. Idempotent. */
export async function ensureSeToken(page: Page): Promise<void> {
  await page.goto("/settings#token");
  await expect(
    page.getByPlaceholder("Paste your JWT token here..."),
  ).toBeVisible();

  const alreadyConfigured = await page
    .getByText("Configured", { exact: true })
    .isVisible();
  if (!alreadyConfigured) {
    await page
      .getByPlaceholder("Paste your JWT token here...")
      .fill("e2e-fake-token");
    await page.getByRole("button", { name: "Save Token" }).click();
    await expect(page.getByText("Token updated.")).toBeVisible();
  }
}

/**
 * Navigates to /stores and waits for the list to settle, then reports whether the named store
 * is already there. Two traps this exists to avoid:
 *
 *  - `isVisible()` does not wait, so probing straight after `goto` races the list fetch and
 *    reports "absent" for a store that exists — which then leads to a `409` on create.
 *  - the probe must be scoped: the Add-store dialog echoes the looked-up channel's display
 *    name, so an unscoped match is satisfied by the dialog itself.
 */
export async function storeIsListed(
  page: Page,
  displayName: string,
): Promise<boolean> {
  await page.goto("/stores");

  // Settle on "the list has rendered", which must NOT be conditioned on this store: another
  // spec's store makes the list non-empty, so "my card or the empty state" can match neither.
  // Every card carries a View-items button, so that plus the empty state covers both states.
  const anyCard = page.getByRole("button", { name: "View items" }).first();
  const empty = page.getByRole("heading", { name: "No stores yet" });
  await expect(anyCard.or(empty).first()).toBeVisible();

  return page.getByRole("main").getByText(displayName).first().isVisible();
}

/**
 * Dismisses the missing-label prompt that opens after a store is created. Waiting for it is
 * also how a caller knows `POST /api/stores` actually finished — and it must be dismissed
 * before any `getByRole` assertion on the page behind it, because the dialog marks that
 * content `aria-hidden`.
 */
export async function dismissMissingLabelPrompt(page: Page): Promise<void> {
  const skip = page.getByRole("button", { name: "Skip for now" });
  await expect(skip).toBeVisible();
  await skip.click();
  await expect(page.getByRole("dialog")).toHaveCount(0);
}

/** The bearer token the app persisted, for the rare assertion that needs a raw API call. */
export async function readAccessToken(page: Page): Promise<string> {
  const token = await page.evaluate((key) => {
    const raw = localStorage.getItem(key);
    return raw
      ? (JSON.parse(raw) as { accessToken?: string }).accessToken
      : null;
  }, TOKEN_KEY);

  expect(token, "the app must have persisted an access token").toBeTruthy();
  return token!;
}
