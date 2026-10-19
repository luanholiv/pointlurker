import { test, expect, type Page } from "@playwright/test";

async function setupAuth(page: Page) {
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
}

test("default theme is dark — data-theme attribute is dark", async ({
  page,
}) => {
  await setupAuth(page);
  await page.goto("/");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
});

test("toggle dark to light — data-theme becomes light", async ({ page }) => {
  await setupAuth(page);
  await page.goto("/");

  const html = page.locator("html");
  await expect(html).toHaveAttribute("data-theme", "dark");

  await page.getByRole("button", { name: /switch to light mode/i }).click();

  await expect(html).toHaveAttribute("data-theme", "light");
});

test("toggle light to dark — data-theme becomes dark", async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem("pl.theme", "light");
  });
  await setupAuth(page);
  await page.goto("/");

  const html = page.locator("html");
  await expect(html).toHaveAttribute("data-theme", "light");

  await page.getByRole("button", { name: /switch to dark mode/i }).click();

  await expect(html).toHaveAttribute("data-theme", "dark");
});

test("theme persists across page reload — pl.theme key in localStorage", async ({
  page,
}) => {
  await page.addInitScript(() => {
    localStorage.setItem("pl.theme", "light");
  });
  await setupAuth(page);
  await page.goto("/");

  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");

  await page.reload();

  await expect(page.locator("html")).toHaveAttribute("data-theme", "light");
  expect(await page.evaluate(() => localStorage.getItem("pl.theme"))).toBe(
    "light",
  );
});
