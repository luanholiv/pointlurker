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

const AVATAR_URL = "https://cdn.example.com/avatar.png";
const THUMBNAIL_URL = "https://cdn.example.com/thumb.png";

const stubBrokenImages = async (page: Page) => {
  // The CDN hosts are fictitious; serve a tiny PNG so the <img> loads (no onError).
  const onePixelPng = Buffer.from(
    "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
    "base64",
  );
  await page.route("https://cdn.example.com/**", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "image/png",
      body: onePixelPng,
    });
  });
};

test("store card renders avatar image from avatarUrl", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await stubBrokenImages(page);

  await page.route("**/api/stores", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              id: "store-1",
              channelId: "ch1",
              displayName: "Test Store",
              isActive: true,
              lastSyncedAt: "2026-03-01T00:00:00Z",
              itemCount: 1,
              avatarUrl: AVATAR_URL,
            },
          ]),
        ),
      });
    } else {
      await route.fallback();
    }
  });

  await page.goto("/stores");

  const avatarImg = page.locator(".store-avatar img");
  await expect(avatarImg).toBeVisible();
  await expect(avatarImg).toHaveAttribute("src", AVATAR_URL);
  await expect(avatarImg).toHaveAttribute("referrerpolicy", "no-referrer");
});

test("item card renders thumbnail image from thumbnailUrl", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await stubBrokenImages(page);

  await page.route("**/api/stores", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              id: "store-1",
              channelId: "ch1",
              displayName: "Test Store",
              isActive: true,
              lastSyncedAt: "2026-03-01T00:00:00Z",
              itemCount: 1,
              avatarUrl: AVATAR_URL,
            },
          ]),
        ),
      });
    } else {
      await route.fallback();
    }
  });

  // The thumbnail travels on the item row, which the grid fetches separately from the store.
  await stubStoreItems(page, "store-1", [
    {
      id: "item-1",
      providerItemId: "se1",
      name: "Cool Item",
      description: "A cool item",
      cost: 500,
      categoryName: "SKIN",
      featured: false,
      subscriberOnly: false,
      allowMessages: false,
      quantityTotal: -1,
      quantityCurrent: -1,
      isAvailable: true,
      wishlistEntryId: null,
      thumbnailUrl: THUMBNAIL_URL,
      providerCreatedAt: "2026-01-01T00:00:00Z",
    },
  ]);

  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: "store-1",
        channelId: "ch1",
        displayName: "Test Store",
        isActive: true,
        lastSyncedAt: "2026-03-01T00:00:00Z",
        createdAt: "2026-03-01T00:00:00Z",
        missingLabels: [],
        avatarUrl: AVATAR_URL,
        itemCount: 1,
      }),
    });
  });

  await page.goto("/stores/store-1");

  const thumbImg = page.locator(".item-media img");
  await expect(thumbImg).toBeVisible();
  await expect(thumbImg).toHaveAttribute("src", THUMBNAIL_URL);
  await expect(thumbImg).toHaveAttribute("referrerpolicy", "no-referrer");
});
