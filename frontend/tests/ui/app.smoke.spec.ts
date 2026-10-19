import { test, expect } from "@playwright/test";

test("app boots to the dashboard shell", async ({ page }) => {
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
      }),
    });
  });

  await page.goto("/");

  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toBeVisible();
});
