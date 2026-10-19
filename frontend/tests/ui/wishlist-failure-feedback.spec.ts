import { test, expect, type Page } from "@playwright/test";
import { fulfillJson, pageOf, stubStoreItems } from "./apiEnvelopes";

/**
 * The wishlist Edit and Remove affordances must report a failed request; catching it and returning
 * leaves the button looking inert rather than failed.
 *
 * The unit tests cover the handlers in jsdom. Four things they structurally cannot cover live here:
 *
 * 1. **The real composition.** Those tests mount the component with a test-supplied
 *    `ToastProvider`. Only a real route proves a toast raised deep in a page reaches the single
 *    host mounted in `App`.
 * 2. **A genuinely failing request.** MSW intercepts inside the test process; `route.abort()`
 *    drops the connection, which is the branch a real network failure takes.
 * 3. **That the toast is visible.** jsdom has no layout, so a toast that is clipped, transparent
 *    or stacked underneath something passes every DOM assertion.
 * 4. **That pt-BR renders.** The catalog tests prove a key exists, not that it reaches the screen.
 *
 * CI runs this project against the built bundle (`npm run preview`), so the visibility assertions
 * are made against the CSS that actually ships.
 */

const ENTRY_ID = "wish-1";

const setupAuth = async (page: Page, locale = "en-US") => {
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
      credentials: [
        {
          provider: "streamelements",
          accountName: null,
          expiresAt: null,
          lastValidatedAt: null,
          lastValidationError: null,
        },
      ],
      locale,
    }),
  );
};

const storeItem = {
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
  wishlistEntryId: ENTRY_ID,
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

const entryRow = {
  id: ENTRY_ID,
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
};

const entryDetail = {
  ...entryRow,
  storeName: "Test Store",
  storeId: "store-1",
  allowMessages: false,
  redemptionMessage: null,
  userInputDefinitions: [] as string[],
  hasStaleInputs: false,
  updatedAt: "2026-04-01T00:00:00Z",
};

/** The store detail route, where an already-wishlisted card's button edits the entry. */
const gotoStoreDetail = async (page: Page) => {
  await stubStoreItems(page, "store-1", [storeItem]);
  await page.route("**/api/stores/store-1/profile", (route) =>
    route.request().method() === "GET"
      ? fulfillJson(route, {
          unresolved: [],
          resolved: [],
          canonicalLabels: [],
        })
      : route.fallback(),
  );
  await page.route("**/api/stores/store-1", (route) =>
    fulfillJson(route, storeDetail),
  );
  await page.goto("/stores/store-1");
};

/** The wishlist route. `rows` is read on every GET so a successful delete can empty it. */
const gotoWishlist = async (page: Page, rows: { entries: object[] }) => {
  await page.route("**/api/wishlist", (route) =>
    route.request().method() === "GET"
      ? fulfillJson(
          route,
          pageOf(
            rows.entries.length
              ? [
                  {
                    storeId: "store-1",
                    storeName: "Test Store",
                    isActive: true,
                    lastSyncedAt: null,
                    entries: rows.entries,
                  },
                ]
              : [],
          ),
        )
      : route.fallback(),
  );
  await page.goto("/wishlist");
};

/**
 * A backend error the frontend will parse into an `ApiError`.
 *
 * The `application/problem+json` content type is load-bearing: `apiClient.parseError` checks it
 * before reading `code`, so plain `application/json` here would discard the code and the assertion
 * would pass against the generic fallback instead of the catalogued reason.
 */
const problem = (status: number, code: string) => ({
  status,
  contentType: "application/problem+json",
  body: JSON.stringify({ status, code, title: "Not Found", params: {} }),
});

const errorToast = (page: Page) => page.locator('li.toast[data-tone="error"]');

test.describe("wishlist failure feedback", () => {
  test("store detail reports a failed entry fetch and keeps the modal shut", async ({
    page,
  }) => {
    await setupAuth(page);
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) =>
      route.fulfill({ status: 500 }),
    );
    await gotoStoreDetail(page);

    await page.getByRole("button", { name: /wishlisted/i }).click();

    await expect(errorToast(page)).toContainText(
      "Failed to load wishlist entry",
    );
    await expect(page.getByRole("dialog")).toHaveCount(0);
  });

  test("store detail shows the reason the backend gave", async ({ page }) => {
    await setupAuth(page);
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) =>
      route.fulfill(problem(404, "wishlist.entry_not_found")),
    );
    await gotoStoreDetail(page);

    await page.getByRole("button", { name: /wishlisted/i }).click();

    await expect(errorToast(page)).toContainText("Wishlist entry not found.");
  });

  // The manual smoke this replaces was "block /api/wishlist/* in DevTools". An aborted request is
  // that: no status code, a raw fetch rejection, which is the branch tApiError falls back on.
  test("wishlist page reports an entry fetch that never completes", async ({
    page,
  }) => {
    await setupAuth(page);
    await gotoWishlist(page, { entries: [entryRow] });
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) => route.abort());

    await page.getByRole("button", { name: "Edit" }).click();

    await expect(errorToast(page)).toContainText(
      "Failed to load wishlist entry",
    );
    await expect(page.getByRole("dialog")).toHaveCount(0);
  });

  test("wishlist page reports a failed remove and keeps the row", async ({
    page,
  }) => {
    await setupAuth(page);
    await gotoWishlist(page, { entries: [entryRow] });
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) =>
      route.request().method() === "DELETE"
        ? route.fulfill(problem(404, "wishlist.entry_not_found"))
        : route.fallback(),
    );

    await page.getByRole("button", { name: "Remove" }).click();

    await expect(errorToast(page)).toContainText("Wishlist entry not found.");
    // A delete that failed must not take the row off screen anyway.
    await expect(page.getByText("Cool Item")).toBeVisible();
  });

  /**
   * The assertion jsdom cannot make. A toast in the DOM but clipped by an overflow, painted
   * under the modal backdrop, or positioned off-screen satisfies every unit test and is invisible
   * to the user — which is the same outcome as the bug.
   */
  test("the toast is actually on screen, not merely in the DOM", async ({
    page,
  }) => {
    await setupAuth(page);
    await gotoWishlist(page, { entries: [entryRow] });
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) =>
      route.fulfill({ status: 500 }),
    );

    await page.getByRole("button", { name: "Edit" }).click();

    const toast = errorToast(page);
    await expect(toast).toBeVisible();

    const box = await toast.boundingBox();
    const viewport = page.viewportSize();
    expect(box).not.toBeNull();
    expect(viewport).not.toBeNull();
    expect(box!.width).toBeGreaterThan(0);
    expect(box!.height).toBeGreaterThan(0);
    expect(box!.x).toBeGreaterThanOrEqual(0);
    expect(box!.y).toBeGreaterThanOrEqual(0);
    expect(box!.x + box!.width).toBeLessThanOrEqual(viewport!.width);
    expect(box!.y + box!.height).toBeLessThanOrEqual(viewport!.height);
  });

  test("pt-BR renders the localized fallback message", async ({ page }) => {
    await setupAuth(page, "pt-BR");
    await gotoWishlist(page, { entries: [entryRow] });
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) =>
      route.fulfill({ status: 500 }),
    );

    await page.getByRole("button", { name: "Editar" }).click();

    await expect(errorToast(page)).toContainText(
      "Falha ao carregar item da lista de desejos",
    );
    await expect(errorToast(page)).toContainText("Erro");
  });

  // Not just a static UI string: this proves a *backend error code* resolves through the catalog
  // in the locale the user is actually on.
  test("pt-BR renders the localized backend reason", async ({ page }) => {
    await setupAuth(page, "pt-BR");
    await gotoWishlist(page, { entries: [entryRow] });
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) =>
      route.request().method() === "DELETE"
        ? route.fulfill(problem(404, "wishlist.entry_not_found"))
        : route.fallback(),
    );

    await page.getByRole("button", { name: "Remover" }).click();

    await expect(errorToast(page)).toContainText(
      "Item da lista de desejos não encontrado.",
    );
  });

  test("a successful edit opens the modal and raises nothing", async ({
    page,
  }) => {
    await setupAuth(page);
    await gotoWishlist(page, { entries: [entryRow] });
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) =>
      route.request().method() === "GET"
        ? fulfillJson(route, entryDetail)
        : route.fallback(),
    );

    await page.getByRole("button", { name: "Edit" }).click();

    await expect(page.getByRole("dialog")).toBeVisible();
    await expect(page.locator("li.toast")).toHaveCount(0);
  });

  test("a successful remove drops the row and raises nothing", async ({
    page,
  }) => {
    const rows = { entries: [entryRow] as object[] };
    await setupAuth(page);
    await gotoWishlist(page, rows);
    await page.route(`**/api/wishlist/${ENTRY_ID}`, (route) => {
      if (route.request().method() !== "DELETE") return route.fallback();
      rows.entries = [];
      return route.fulfill({ status: 204 });
    });

    await expect(page.getByText("Cool Item")).toBeVisible();
    await page.getByRole("button", { name: "Remove" }).click();

    await expect(page.getByText("Your wishlist is empty")).toBeVisible();
    await expect(page.locator("li.toast")).toHaveCount(0);
  });
});
