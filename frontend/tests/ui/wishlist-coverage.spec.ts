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

test("add new store with missing labels prompts dialog and saves profile values", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/stores/lookup*", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        externalId: "ch1",
        handle: "teststreamer",
        displayName: "Test Store",
        avatarUrl: null,
        itemCount: null,
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
    } else if (route.request().method() === "POST") {
      await route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify({
          id: "store-1",
          channelId: "ch1",
          displayName: "Test Store",
          isActive: true,
          lastSyncedAt: null,
          createdAt: "2026-03-01T00:00:00Z",
          missingLabels: ["Twitch Username"],
          itemCount: 0,
        }),
      });
    }
  });

  let upsertRequestBody: unknown = null;
  await page.route("**/api/stores/store-1/profile/values", async (route) => {
    if (route.request().method() === "PUT") {
      upsertRequestBody = route.request().postDataJSON();
      await route.fulfill({ status: 204 });
    } else {
      await route.fallback();
    }
  });

  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();
  await page.getByPlaceholder("e.g. xqc").fill("teststreamer");
  await page.getByRole("button", { name: "Lookup" }).click();
  await expect(page.getByRole("button", { name: /Continue/i })).toBeEnabled();
  await page.getByRole("button", { name: /Continue/i }).click();

  // MissingLabelDialog appears
  await expect(
    page.getByRole("heading", { name: /fill store profile/i }),
  ).toBeVisible();
  await expect(page.getByTestId("missing-label-Twitch Username")).toBeVisible();

  await page.getByLabel("Twitch Username").fill("xqc");
  await page.getByRole("button", { name: /save profile/i }).click();

  // Dialog closes after save
  await expect(
    page.getByRole("heading", { name: /fill store profile/i }),
  ).not.toBeVisible();

  // PUT body verified
  expect(upsertRequestBody).toEqual({
    values: [{ labelCanonical: "Twitch Username", value: "xqc" }],
  });
});

test("wishlist add with autoRedeem on uncovered profile shows 422 error", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await stubStoreItems(page, "store-1", [storeDetailItem]);

  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(storeDetail),
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

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() === "POST") {
      // problem+json, as the backend sends it: the client only parses the `{code, params}`
      // extensions off that media type, so a contract body labelled `application/json` was being
      // discarded and the screen fell back to the transport's own status line.
      await route.fulfill({
        status: 422,
        contentType: "application/problem+json",
        body: JSON.stringify({
          type: "https://errors.app/wishlist/missing_input_coverage",
          title:
            "Auto-redeem requires the store profile to cover all item input labels.",
          status: 422,
          code: "wishlist.missing_input_coverage",
          params: { missingLabels: ["Discord Tag"] },
        }),
      });
    } else {
      await route.fallback();
    }
  });

  await page.goto("/stores/store-1");
  await page.getByRole("button", { name: "Add to Wishlist" }).click();

  await expect(
    page.getByRole("heading", { name: /add to wishlist/i }),
  ).toBeVisible();

  // Enable auto-redeem
  await page
    .getByRole("switch", { name: "Auto-redeem when available" })
    .click();

  // Submit
  await page.getByRole("button", { name: "Add to Wishlist" }).last().click();

  // UI surfaces failure: error text visible (status 422 or banner referencing the missing label)
  const errorOrBanner = page
    .getByText(/422|Discord Tag|blocked|missing/i)
    .first();
  await expect(errorOrBanner).toBeVisible();
});
