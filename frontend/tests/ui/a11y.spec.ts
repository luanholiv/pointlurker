import { test, expect, type Page } from "@playwright/test";
import { pageOf, stubStoreItems } from "./apiEnvelopes";
import AxeBuilder from "@axe-core/playwright";

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

  await page.route("**/api/stores", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(pageOf([])),
      });
    } else {
      await route.fallback();
    }
  });

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(pageOf([])),
      });
    } else {
      await route.fallback();
    }
  });

  await page.route("**/api/redemptions*", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ items: [], totalCount: 0, page: 1, pageSize: 20 }),
    });
  });
};

const storeDetail = {
  id: "store-1",
  channelId: "ch1",
  displayName: "Test Store",
  isActive: true,
  lastSyncedAt: "2026-03-01T00:00:00Z",
  createdAt: "2026-03-01T00:00:00Z",
  missingLabels: [] as string[],
  itemCount: 1,
};

// The grid fetches its own slice, so the cards the audit walks come from here.
const storeItems = [
  {
    id: "item-1",
    providerItemId: "se1",
    name: "Cool Item",
    description: "A cool item",
    cost: 500,
    categoryName: "rewards",
    featured: false,
    subscriberOnly: false,
    allowMessages: false,
    quantityTotal: -1,
    quantityCurrent: -1,
    isAvailable: true,
    thumbnailUrl: null,
    wishlistEntryId: null,
    providerCreatedAt: "2026-01-01T00:00:00Z",
  },
];

const setupStoreProfile = async (page: Page, storeId = "store-1") => {
  await stubStoreItems(page, storeId, storeItems);

  await page.route(`**/api/stores/${storeId}/profile`, async (route) => {
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

test("/stores page passes WCAG 2.1 A", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await page.goto("/stores");
  await expect(page.locator("main")).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a"]).analyze();

  expect(results.violations).toEqual([]);
});

test("/stores with AddStoreDialog open passes WCAG 2.1 A", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();
  await expect(page.getByRole("dialog")).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a"]).analyze();

  expect(results.violations).toEqual([]);
});

test("/stores/:id page passes WCAG 2.1 A", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await setupStoreProfile(page);
  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(storeDetail),
    });
  });
  await page.goto("/stores/store-1");
  await expect(page.getByText("Cool Item", { exact: true })).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a"]).analyze();

  expect(results.violations).toEqual([]);
});

test("/wishlist page passes WCAG 2.1 A", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await page.goto("/wishlist");
  await expect(page.locator("main")).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a"]).analyze();

  expect(results.violations).toEqual([]);
});

test("/wishlist with WishlistEntryModal open passes WCAG 2.1 A", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreProfile(page);

  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(storeDetail),
    });
  });

  await page.goto("/stores/store-1");
  await page.getByRole("button", { name: "Add to Wishlist" }).click();
  await expect(page.getByRole("dialog")).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a"]).analyze();

  expect(results.violations).toEqual([]);
});

test("/settings page passes WCAG 2.1 A", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await page.goto("/settings");
  await expect(page.locator("main")).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a"]).analyze();

  expect(results.violations).toEqual([]);
});

test("/redemptions page passes WCAG 2.1 A", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await page.goto("/redemptions");
  await expect(page.locator("main")).toBeVisible();

  const results = await new AxeBuilder({ page }).withTags(["wcag2a"]).analyze();

  expect(results.violations).toEqual([]);
});
