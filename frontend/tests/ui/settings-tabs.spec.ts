import { test, expect, type Page } from "@playwright/test";

const setupAuthenticatedUser = async (page: Page) => {
  await page.addInitScript(() => {
    localStorage.setItem(
      "pl.auth.token",
      JSON.stringify({
        accessToken: "token",
        refreshToken: "refresh",
        expiresAt: Date.now() + 3600000,
      }),
    );
  });

  await page.route("**/api/auth/bootstrap", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userExists: true }),
    });
  });

  await page.route("**/api/users/me", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        userId: "user-1",
        email: "admin@example.com",
        roles: ["Admin"],
        credentials: [],
        locale: "en-US",
      }),
    });
  });
};

const accountTab = (page: Page) => page.getByRole("tab", { name: "Account" });
const tokenTab = (page: Page) =>
  page.getByRole("tab", { name: "Store credentials" });
const securityTab = (page: Page) => page.getByRole("tab", { name: "Security" });

const updatePasswordButton = (page: Page) =>
  page.getByRole("button", { name: /Update password/i });
const jwtInput = (page: Page) =>
  page.getByPlaceholder("Paste your JWT token here...");

test("8.1 settings defaults to Account tab with no hash", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.goto("/settings");
  await expect(accountTab(page)).toHaveAttribute("aria-selected", "true");
  expect(new URL(page.url()).hash).toBe("");
  await expect(updatePasswordButton(page)).toHaveCount(0);
});

test("8.2 clicking Security shows content and sets #security without reload", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await page.goto("/settings");
  await securityTab(page).click();
  await expect(securityTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(updatePasswordButton(page)).toBeVisible();
  expect(new URL(page.url()).hash).toBe("#security");
});

test("8.3 deep link /settings#security opens Security tab", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await page.goto("/settings#security");
  await expect(securityTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(updatePasswordButton(page)).toBeVisible();
});

test("8.4 full page reload on #token restores the store credentials tab", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await page.goto("/settings#token");
  await expect(jwtInput(page)).toBeVisible();

  await page.reload();
  await expect(tokenTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(jwtInput(page)).toBeVisible();
});

test("8.5 unknown hash falls back to Account tab", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.goto("/settings#bogus");
  await expect(accountTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(updatePasswordButton(page)).toHaveCount(0);
});
