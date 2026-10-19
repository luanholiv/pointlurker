import { test, expect, type Page } from "@playwright/test";
import { stubStoreItems } from "./apiEnvelopes";

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
        credentials: [
          {
            provider: "streamelements",
            accountName: null,
            expiresAt: null,
            lastValidatedAt: null,
            lastValidationError: null,
          },
        ],
      }),
    });
  });
};

const storeItems = [
  {
    id: "item-1",
    providerItemId: "se1",
    name: "Golden Sword",
    description: null,
    type: "perk",
    cost: 500,
    categoryName: "Weapons",
    enabled: true,
    featured: false,
    isPublic: true,
    subscriberOnly: false,
    allowMessages: false,
    userInputDefinitions: [],
    quantityTotal: 10,
    quantityCurrent: 5,
    isAvailable: true,
    wishlistEntryId: null,
  },
  {
    id: "item-2",
    providerItemId: "se2",
    name: "Silver Shield",
    description: null,
    type: "perk",
    cost: 200,
    categoryName: "Armor",
    enabled: false,
    featured: false,
    isPublic: true,
    subscriberOnly: true,
    allowMessages: false,
    userInputDefinitions: [],
    quantityTotal: 5,
    quantityCurrent: 0,
    isAvailable: false,
    wishlistEntryId: null,
  },
  {
    id: "item-3",
    providerItemId: "se3",
    name: "Gold Ring",
    description: null,
    type: "perk",
    cost: 1000,
    categoryName: "Jewelry",
    enabled: true,
    featured: true,
    isPublic: true,
    subscriberOnly: false,
    allowMessages: false,
    userInputDefinitions: [],
    quantityTotal: -1,
    quantityCurrent: -1,
    isAvailable: true,
    wishlistEntryId: null,
  },
  {
    id: "item-4",
    providerItemId: "se4",
    name: "Iron Helm",
    description: null,
    type: "perk",
    cost: 100,
    categoryName: "Armor",
    enabled: false,
    featured: true,
    isPublic: true,
    subscriberOnly: true,
    allowMessages: false,
    userInputDefinitions: [],
    quantityTotal: 3,
    quantityCurrent: 1,
    isAvailable: false,
    wishlistEntryId: null,
  },
];

const storeResponse = {
  id: "store-1",
  channelId: "ch1",
  displayName: "Test Store",
  isActive: true,
  lastSyncedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  itemCount: storeItems.length,
  missingLabels: [] as string[],
};

const setupStoreDetail = async (page: Page) => {
  // The stub narrows and orders from the query string alone, so every assertion below now also
  // proves the browser asked the server for that view rather than re-filtering rows it held.
  await stubStoreItems(page, "store-1", storeItems);

  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(storeResponse),
    });
  });

  await page.route("**/api/stores/store-1/profile", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          unresolved: [],
          resolved: [],
          canonicalLabels: [],
        }),
      });
    } else {
      await route.fallback();
    }
  });
};

test("filter by availability — default ON shows only available items", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1");

  // Default: available=true, only available items shown
  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Gold Ring")).toBeVisible();
  await expect(page.getByText("Silver Shield")).not.toBeVisible();
  await expect(page.getByText("Iron Helm")).not.toBeVisible();
});

test("Search button shows and hides filter panel", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1");

  await expect(
    page.getByRole("searchbox", { name: /search items/i }),
  ).not.toBeVisible();
  await page.getByRole("button", { name: "Search" }).click();
  await expect(
    page.getByRole("searchbox", { name: /search items/i }),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Available" })).toBeVisible();
  await page.getByRole("button", { name: "Search" }).click();
  await expect(
    page.getByRole("searchbox", { name: /search items/i }),
  ).not.toBeVisible();
});

test("filter by availability — toggle OFF shows all items", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1");
  await page.getByRole("button", { name: "Search" }).click();

  await page.getByRole("button", { name: "Available" }).click();

  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Silver Shield")).toBeVisible();
  await expect(page.getByText("Gold Ring")).toBeVisible();
  await expect(page.getByText("Iron Helm")).toBeVisible();
});

test("search by name — type partial name, grid narrows to matches", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1?avail=0");
  await page.getByRole("button", { name: "Search" }).click();

  await page.getByRole("searchbox", { name: /search items/i }).fill("gold");

  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Gold Ring")).toBeVisible();
  await expect(page.getByText("Silver Shield")).not.toBeVisible();
  await expect(page.getByText("Iron Helm")).not.toBeVisible();
});

test("filter by category — select category, only matching items shown", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1?avail=0");
  await page.getByRole("button", { name: "Search" }).click();

  await page
    .locator('[aria-label="Category filter"]')
    .getByRole("button", { name: "Armor" })
    .click();

  await expect(page.getByText("Silver Shield")).toBeVisible();
  await expect(page.getByText("Iron Helm")).toBeVisible();
  await expect(page.getByText("Golden Sword")).not.toBeVisible();
  await expect(page.getByText("Gold Ring")).not.toBeVisible();
});

test("filter by cost range — items outside range hidden", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1?avail=0");
  await page.getByRole("button", { name: "Search" }).click();

  await page.getByRole("spinbutton", { name: /minimum cost/i }).fill("300");
  await page.getByRole("spinbutton", { name: /minimum cost/i }).press("Tab");
  // Wait for React to process min before filling max (prevents stale-prev race in setSearchParams)
  await page.waitForURL(/min=300/);
  // waitForURL only proves the navigation landed, not that the grid re-rendered with it.
  // Wait for a positive effect of min — Gold Ring costs 1000, so min=300 keeps it — before
  // typing max. Otherwise max's setSearchParams can close over a stale prev and drop min,
  // leaving the grid filtered by max alone with Iron Helm (cost 100) still showing.
  await expect(page.getByText("Gold Ring")).toBeVisible();
  await page.getByRole("spinbutton", { name: /maximum cost/i }).fill("600");
  await page.getByRole("spinbutton", { name: /maximum cost/i }).press("Tab");
  await page.waitForURL(/max=600/);
  // A dropped min is the actual flake, and it still matches /max=600/ — assert both bounds
  // survived rather than trusting the max match alone.
  await expect(page).toHaveURL(/min=300/);

  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Silver Shield")).not.toBeVisible();
  await expect(page.getByText("Gold Ring")).not.toBeVisible();
  await expect(page.getByText("Iron Helm")).not.toBeVisible();
});

test("filter by subscriber-only — toggle ON, only sub-only items shown", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1?avail=0");
  await page.getByRole("button", { name: "Search" }).click();

  await page.getByRole("button", { name: "Sub Only" }).first().click();

  await expect(page.getByText("Silver Shield")).toBeVisible();
  await expect(page.getByText("Iron Helm")).toBeVisible();
  await expect(page.getByText("Golden Sword")).not.toBeVisible();
  await expect(page.getByText("Gold Ring")).not.toBeVisible();
});

test("sort by cost descending — items ordered highest cost first", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1?avail=0");
  await page.getByRole("button", { name: "Search" }).click();

  await page
    .getByRole("combobox", { name: /sort field/i })
    .selectOption("cost");
  await page.waitForURL(/sort=cost/);
  // Wait for React to re-render with cost-ascending sort before changing direction
  // (Iron Helm cost=100 is first in ascending cost order)
  await expect(page.locator(".item-card .title").first()).toHaveText(
    "Iron Helm",
  );
  await page
    .getByRole("combobox", { name: /sort direction/i })
    .selectOption("desc");
  await page.waitForURL(/dir=desc/);

  await expect(page.locator(".item-card .title")).toHaveText([
    "Gold Ring",
    "Golden Sword",
    "Silver Shield",
    "Iron Helm",
  ]);
});

test("URL persistence — apply filters, reload page, same filters applied", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1?avail=0&q=gold&sort=cost&dir=desc");
  await page.getByRole("button", { name: "Search" }).click();

  await expect(
    page.getByRole("searchbox", { name: /search items/i }),
  ).toHaveValue("gold");
  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Gold Ring")).toBeVisible();
  await expect(page.getByText("Silver Shield")).not.toBeVisible();

  // Reload and check filters persist from URL (panel starts closed after reload)
  await page.reload();

  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Gold Ring")).toBeVisible();
  await expect(page.getByText("Silver Shield")).not.toBeVisible();
});

test("reset all — apply multiple filters, click reset, grid returns to default view", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreDetail(page);

  await page.goto("/stores/store-1?avail=0&sub=1&sort=cost");

  // Multiple filters applied — verify
  await expect(page.getByText("Silver Shield")).toBeVisible();
  await expect(page.getByText("Golden Sword")).not.toBeVisible();

  await page.getByRole("button", { name: "Search" }).click();
  await page.getByRole("button", { name: /reset all/i }).click();

  // After reset: default available=true, only available items shown
  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Gold Ring")).toBeVisible();
  await expect(page.getByText("Silver Shield")).not.toBeVisible();

  // URL params cleared
  await expect(page).toHaveURL(/\/stores\/store-1$/);
});
