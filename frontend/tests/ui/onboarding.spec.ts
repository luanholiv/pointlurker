import { test, expect, type Page } from "@playwright/test";
import { fulfillJson, pageOf, stubStoreSummary } from "./apiEnvelopes";

const signIn = async (page: Page, hasCredential: boolean) => {
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

  await page.route("**/api/auth/bootstrap", (route) =>
    fulfillJson(route, { userExists: true }),
  );
  await page.route("**/api/users/me", (route) =>
    fulfillJson(route, {
      userId: "user-1",
      email: "admin@example.com",
      credentials: hasCredential
        ? [
            {
              provider: "streamelements",
              accountName: null,
              expiresAt: null,
              lastValidatedAt: null,
              lastValidationError: null,
            },
          ]
        : [],
      twoFactorEnabled: false,
      locale: "en-US",
    }),
  );
  await page.route("**/api/stores?*", (route) =>
    fulfillJson(route, pageOf([])),
  );
  await page.route("**/api/stores", (route) => fulfillJson(route, pageOf([])));
};

const stubWishlist = (page: Page, groups: unknown[]) =>
  page.route("**/api/wishlist", (route) => fulfillJson(route, pageOf(groups)));

const wishlistGroup = {
  storeId: "store-1",
  storeName: "Test Store",
  isActive: true,
  lastSyncedAt: null,
  entries: [
    {
      id: "w1",
      storeItemId: "i1",
      itemName: "Item",
      itemCost: 100,
      isAvailable: false,
      notifyOnAvailable: true,
      autoRedeem: false,
      desiredRedemptions: 1,
      quotaUsed: 0,
      isBlocked: false,
      missingLabels: [],
      createdAt: "2026-10-01T00:00:00Z",
    },
  ],
};

test("a fresh install lands on the setup checklist and is led to credentials", async ({
  page,
}) => {
  await signIn(page, false);
  await stubStoreSummary(page, []);
  await stubWishlist(page, []);

  await page.goto("/");

  await expect(
    page.getByRole("heading", { level: 1, name: "Get started" }),
  ).toBeVisible();
  await expect(page.getByText("0 of 3 done")).toBeVisible();
  await expect(page.getByRole("link", { name: /Finish setup/ })).toContainText(
    "0/3",
  );

  await page.getByRole("link", { name: "Open credentials" }).click();

  await expect(page).toHaveURL(/\/settings#token$/);
  await expect(
    page.getByText(/streamelements\.com\/dashboard\/account\/channels/),
  ).toBeVisible();
});

test("a connected provider moves the checklist on to adding a store", async ({
  page,
}) => {
  await signIn(page, true);
  await stubStoreSummary(page, []);
  await stubWishlist(page, []);

  await page.goto("/");

  await expect(page.getByText("1 of 3 done")).toBeVisible();
  await expect(
    page.getByRole("link", { name: "Open credentials" }),
  ).toHaveCount(0);
  await page.getByRole("link", { name: "Add a store" }).click();
  await expect(page).toHaveURL(/\/stores$/);
});

test("a completed setup offers auto-redeem, then finishes for good", async ({
  page,
}) => {
  await signIn(page, true);
  await stubStoreSummary(page, [{ isActive: true }]);
  await stubWishlist(page, [wishlistGroup]);

  await page.goto("/");

  await expect(
    page.getByRole("heading", { name: "You're all set" }),
  ).toBeVisible();
  await expect(page.getByText("3 of 3 done")).toBeVisible();
  await expect(
    page.getByRole("link", { name: /Setup complete/ }),
  ).toBeVisible();

  await page.getByRole("link", { name: "Open wishlist" }).click();
  await expect(page).toHaveURL(/\/wishlist$/);

  await page.goto("/");
  await page.getByRole("button", { name: "Finish setup" }).click();

  await expect(page).toHaveURL(/\/stores$/);
  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: /Setup complete/ })).toHaveCount(
    0,
  );

  await page.goto("/");
  await expect(page).toHaveURL(/\/stores$/);
});

test("a finished setup goes straight to the stores and shows no setup progress", async ({
  page,
}) => {
  await signIn(page, true);
  await page.addInitScript(() =>
    localStorage.setItem("pl.setup.dismissed", "1"),
  );
  await stubStoreSummary(page, [{ isActive: true }]);
  await stubWishlist(page, [wishlistGroup]);

  await page.goto("/");

  await expect(page).toHaveURL(/\/stores$/);
  await expect(
    page.getByRole("heading", { level: 1, name: "Stores" }),
  ).toBeVisible();
  await expect(page.getByRole("link", { name: /Finish setup/ })).toHaveCount(0);
});
