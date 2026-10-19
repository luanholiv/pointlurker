import { test, expect } from "@playwright/test";

test("bootstrap flow creates admin and redirects to login", async ({
  page,
}) => {
  let registered = false;

  await page.route("**/api/auth/bootstrap", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userExists: registered }),
    });
  });

  await page.route("**/api/auth/register", async (route) => {
    registered = true;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userId: "user-id" }),
    });
  });

  await page.goto("/bootstrap");

  await page.getByLabel("Email").fill("admin@example.com");
  await page.getByLabel("Password", { exact: true }).fill("Password123!");
  await page.getByLabel("Confirm new password").fill("Password123!");
  await page.getByRole("button", { name: "Create admin" }).click();

  await expect(page.getByRole("heading", { name: "Sign in" })).toBeVisible();
});

test("login flow prompts for two-factor code", async ({ page }) => {
  await page.route("**/api/auth/bootstrap", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userExists: true }),
    });
  });

  await page.route("**/api/auth/login", async (route) => {
    const payload = route.request().postDataJSON() as {
      twoFactorCode?: string | null;
    };

    if (!payload?.twoFactorCode) {
      // What the backend sends for a second-factor challenge: the Identity `detail` plus the
      // contract code the middleware adds to it, which is the signal the client keys on.
      await route.fulfill({
        status: 401,
        contentType: "application/problem+json",
        body: JSON.stringify({
          status: 401,
          detail: "RequiresTwoFactor",
          code: "auth.two_factor_required",
          params: {},
        }),
      });
      return;
    }

    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        accessToken: "token",
        refreshToken: "refresh",
        tokenType: "Bearer",
        expiresIn: 3600,
      }),
    });
  });

  await page.route("**/api/users/me", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        userId: "user",
        email: "admin@example.com",
        roles: ["Admin"],
        credentials: [],
      }),
    });
  });

  await page.goto("/login");

  await page.getByLabel("Email").fill("admin@example.com");
  await page.getByLabel("Password").fill("Password123!");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page.getByLabel("Authenticator code")).toBeVisible();
  await page.getByLabel("Authenticator code").fill("123456");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toBeVisible();
});

test("settings page renders for authenticated users", async ({ page }) => {
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
        userId: "user",
        email: "admin@example.com",
        roles: ["Admin"],
        credentials: [],
      }),
    });
  });

  await page.goto("/settings");

  await expect(
    page.getByRole("heading", { level: 1, name: "Settings" }),
  ).toBeVisible();

  await page.getByRole("tab", { name: "Security" }).click();
  await expect(page.getByText("Change password")).toBeVisible();
  await expect(page.getByText("Two-factor auth")).toBeVisible();
});
