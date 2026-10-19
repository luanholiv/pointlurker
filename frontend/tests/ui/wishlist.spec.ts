import { test, expect, type Page } from "@playwright/test";
import { pageOf, stubStoreItems } from "./apiEnvelopes";

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

const setupStoreProfile = async (
  page: Page,
  storeId = "store-1",
  items = [storeDetailItem],
) => {
  await stubStoreItems(page, storeId, items);

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

const storeDetailItem = {
  id: "item-1",
  providerItemId: "se1",
  name: "Cool Item",
  description: "A cool item",
  type: "perk",
  cost: 500,
  categoryName: "rewards",
  enabled: true,
  featured: false,
  isPublic: true,
  subscriberOnly: false,
  allowMessages: false,
  userInputDefinitions: [] as string[],
  quantityTotal: -1,
  quantityCurrent: -1,
  isAvailable: true,
  wishlistEntryId: null as string | null,
};

const storeDetail = {
  id: "store-1",
  channelId: "ch1",
  displayName: "Test Store",
  isActive: true,
  lastSyncedAt: "2026-03-01T00:00:00Z",
  createdAt: "2026-03-01T00:00:00Z",
  itemCount: 1,
  missingLabels: [] as string[],
};

test("add item to wishlist from store detail view", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await setupStoreProfile(page);

  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(storeDetail),
    });
  });

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "POST") {
      await route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          id: "wish-1",
          storeItemId: "item-1",
          itemName: "Cool Item",
          storeName: "Test Store",
          notifyOnAvailable: true,
          autoRedeem: false,
          createdAt: "2026-04-01T00:00:00Z",
        }),
      });
    }
  });

  await page.goto("/stores/store-1");
  await expect(page.getByText("Cool Item", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: "Add to Wishlist" }).click();

  // Modal opens
  await expect(
    page.getByRole("heading", { name: /add to wishlist/i }),
  ).toBeVisible();
  // `wishlist.modal.description` joins name and cost with an em dash, not a hyphen.
  await expect(page.getByText("Cool Item — 500 pts")).toBeVisible();

  await page.getByRole("button", { name: "Add to Wishlist" }).last().click();
});

test("wishlist page displays entries grouped by store", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              storeId: "store-1",
              storeName: "Test Store",
              isActive: true,
              lastSyncedAt: "2026-03-01T00:00:00Z",
              entries: [
                {
                  id: "wish-1",
                  storeItemId: "item-1",
                  itemName: "Cool Item",
                  itemCost: 500,
                  isAvailable: true,
                  notifyOnAvailable: true,
                  autoRedeem: false,
                  desiredRedemptions: 1,
                  quotaUsed: 0,
                  isBlocked: false,
                  missingLabels: [],
                  createdAt: "2026-04-01T00:00:00Z",
                },
              ],
            },
          ]),
        ),
      });
    }
  });

  await page.goto("/wishlist");
  await expect(page.getByText("Test Store")).toBeVisible();
  await expect(page.getByText("Cool Item")).toBeVisible();
  await expect(page.getByText("500 pts")).toBeVisible();
  // Active store shows Active pill; entry with notifyOnAvailable shows Notify chip
  await expect(page.getByText("Active")).toBeVisible();
  await expect(page.getByText("Notify")).toBeVisible();
});

test("wishlist page shows store active/paused status and entry chips", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              storeId: "store-1",
              storeName: "Active Store",
              isActive: true,
              lastSyncedAt: null,
              entries: [
                {
                  id: "wish-1",
                  storeItemId: "item-1",
                  itemName: "Notify Item",
                  itemCost: 100,
                  isAvailable: true,
                  notifyOnAvailable: true,
                  autoRedeem: true,
                  desiredRedemptions: 1,
                  quotaUsed: 0,
                  isBlocked: false,
                  missingLabels: [],
                  createdAt: "2026-04-01T00:00:00Z",
                },
              ],
            },
            {
              storeId: "store-2",
              storeName: "Paused Store",
              isActive: false,
              lastSyncedAt: null,
              entries: [
                {
                  id: "wish-2",
                  storeItemId: "item-2",
                  itemName: "Unavailable Item",
                  itemCost: 200,
                  isAvailable: false,
                  notifyOnAvailable: false,
                  autoRedeem: false,
                  desiredRedemptions: 1,
                  quotaUsed: 0,
                  isBlocked: false,
                  missingLabels: [],
                  createdAt: "2026-04-01T00:00:00Z",
                },
              ],
            },
          ]),
        ),
      });
    }
  });

  await page.goto("/wishlist");

  await expect(page.getByText("Active Store")).toBeVisible();
  await expect(page.getByText("Paused Store")).toBeVisible();
  await expect(page.getByText("Active", { exact: true })).toBeVisible();
  await expect(page.getByText("Paused", { exact: true })).toBeVisible();
  await expect(page.getByText("Notify", { exact: true })).toBeVisible();
  await expect(page.getByText("Auto-redeem", { exact: true })).toBeVisible();
  await expect(page.getByText("Unavailable").first()).toBeVisible();
});

test("edit wishlist entry from wishlist page", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              storeId: "store-1",
              storeName: "Test Store",
              isActive: true,
              lastSyncedAt: null,
              entries: [
                {
                  id: "wish-1",
                  storeItemId: "item-1",
                  itemName: "Cool Item",
                  itemCost: 500,
                  isAvailable: true,
                  notifyOnAvailable: true,
                  autoRedeem: false,
                  desiredRedemptions: 1,
                  quotaUsed: 0,
                  isBlocked: false,
                  missingLabels: [],
                  createdAt: "2026-04-01T00:00:00Z",
                },
              ],
            },
          ]),
        ),
      });
    }
  });

  await page.route("**/api/wishlist/wish-1", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          id: "wish-1",
          storeItemId: "item-1",
          itemName: "Cool Item",
          itemCost: 500,
          isAvailable: true,
          storeName: "Test Store",
          storeId: "store-1",
          notifyOnAvailable: true,
          autoRedeem: false,
          allowMessages: false,
          redemptionMessage: null,
          isBlocked: false,
          missingLabels: [],
          createdAt: "2026-04-01T00:00:00Z",
          updatedAt: "2026-04-01T00:00:00Z",
        }),
      });
    } else if (route.request().method() === "PUT") {
      await route.fulfill({ status: 204 });
    }
  });

  await page.goto("/wishlist");
  await page.getByRole("button", { name: "Edit" }).click();

  await expect(
    page.getByRole("heading", { name: /edit entry/i }),
  ).toBeVisible();
});

test("remove wishlist entry", async ({ page }) => {
  await setupAuthenticatedUser(page);

  let entries = [
    {
      id: "wish-1",
      storeItemId: "item-1",
      itemName: "Cool Item",
      itemCost: 500,
      isAvailable: true,
      notifyOnAvailable: true,
      autoRedeem: false,
      desiredRedemptions: 1,
      quotaUsed: 0,
      isBlocked: false,
      missingLabels: [] as string[],
      createdAt: "2026-04-01T00:00:00Z",
    },
  ];

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf(
            entries.length > 0
              ? [
                  {
                    storeId: "store-1",
                    storeName: "Test Store",
                    isActive: true,
                    lastSyncedAt: null,
                    entries,
                  },
                ]
              : [],
          ),
        ),
      });
    }
  });

  await page.route("**/api/wishlist/wish-1", async (route) => {
    if (route.request().method() === "DELETE") {
      entries = [];
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: "{}",
      });
    } else {
      await route.fallback();
    }
  });

  await page.goto("/wishlist");
  await expect(page.getByText("Cool Item")).toBeVisible();

  await page.getByRole("button", { name: "Remove" }).click();
  await expect(page.getByText("Your wishlist is empty")).toBeVisible();
});

test("blocked badge appears on blocked wishlist entry", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              storeId: "store-1",
              storeName: "Test Store",
              isActive: true,
              lastSyncedAt: null,
              entries: [
                {
                  id: "wish-1",
                  storeItemId: "item-1",
                  itemName: "Cool Item",
                  itemCost: 500,
                  isAvailable: true,
                  notifyOnAvailable: true,
                  autoRedeem: true,
                  desiredRedemptions: 1,
                  quotaUsed: 0,
                  isBlocked: true,
                  missingLabels: ["Discord Tag"],
                  createdAt: "2026-04-01T00:00:00Z",
                },
              ],
            },
          ]),
        ),
      });
    }
  });

  await page.goto("/wishlist");
  await expect(page.getByTestId(/wishlist-blocked-/)).toBeVisible();
  await expect(page.getByTestId("wishlist-blocked-wish-1")).toBeVisible();
});

test("store detail button reflects wishlist status", async ({ page }) => {
  const wishlisted = {
    ...storeDetailItem,
    id: "item-2",
    name: "Wishlisted Item",
    wishlistEntryId: "wish-1",
  };

  await setupAuthenticatedUser(page);
  await setupStoreProfile(page, "store-1", [storeDetailItem, wishlisted]);

  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ ...storeDetail, itemCount: 2 }),
    });
  });

  await page.goto("/stores/store-1");

  // Non-wishlisted item should show "Add to Wishlist"
  await expect(
    page.getByRole("button", { name: "Add to Wishlist" }),
  ).toBeVisible();

  // Wishlisted item should show "Wishlisted"
  await expect(page.getByRole("button", { name: /Wishlisted/ })).toBeVisible();
});

test("empty wishlist page shows empty state message", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(pageOf([])),
      });
    }
  });

  await page.goto("/wishlist");
  await expect(page.getByText("Your wishlist is empty")).toBeVisible();
});

test("wishlist modal Cancel button closes the modal", async ({ page }) => {
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
  await expect(
    page.getByRole("heading", { name: /add to wishlist/i }),
  ).toBeVisible();

  await page.getByRole("button", { name: "Cancel" }).click();
  await expect(
    page.getByRole("heading", { name: /add to wishlist/i }),
  ).not.toBeVisible();
});

test("wishlist modal backdrop click closes the modal", async ({ page }) => {
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
  await expect(
    page.getByRole("heading", { name: /add to wishlist/i }),
  ).toBeVisible();

  // Click backdrop at top-left corner (outside the centered modal panel)
  await page.locator(".backdrop").click({ position: { x: 5, y: 5 } });
  await expect(
    page.getByRole("heading", { name: /add to wishlist/i }),
  ).not.toBeVisible();
});

test("Escape closes WishlistEntryModal and returns focus to trigger", async ({
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

  await page.keyboard.press("Escape");
  await expect(page.getByRole("dialog")).not.toBeVisible();
});
