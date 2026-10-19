import { test, expect, type Page } from "@playwright/test";
import {
  ensureSeToken,
  forbidPageRoute,
  loginAsAdmin,
  readAccessToken,
  storeIsListed,
} from "./realStack";

// Real, UNMOCKED search: filtering, sorting and paging now happen in PostgreSQL, so this is the
// only test that proves the whole chain — browser URL → query string → SQL → rendered grid —
// actually lines up. A mocked test would only prove the client can render a list.
//
// Re-entrant like every `real` spec: the project runs with one retry and no database reset, so
// the store is created only when it is not already there.

const SE_USERNAME = "e2e_bulk";
// Must match wiremock/__files/channel-response-bulk.json displayName.
const STORE_DISPLAY_NAME = "E2E Bulk Store";
// The fixture holds 60 items; the grid asks for 48 at a time.
const TOTAL_ITEMS = 60;
const PAGE_SIZE = 48;
// Round-robin over three categories in the fixture, so item n lands in n % 3.
const BADGES_ITEMS = 20;

test.beforeEach(async ({ page }) => {
  test.setTimeout(180_000);
  forbidPageRoute(page, "store-item-search.real.spec.ts");
});

/** Creates the bulk store once, then navigates to its detail page and returns that URL. */
async function openBulkStore(page: Page): Promise<string> {
  await loginAsAdmin(page);
  await ensureSeToken(page);

  if (!(await storeIsListed(page, STORE_DISPLAY_NAME))) {
    await page.getByRole("button", { name: "Add Store" }).first().click();
    await page.getByPlaceholder("e.g. xqc").fill(SE_USERNAME);
    await page.getByRole("button", { name: "Lookup" }).click();
    await expect(
      page.getByRole("dialog").getByText(STORE_DISPLAY_NAME),
    ).toBeVisible();
    await page.getByRole("button", { name: /Continue/i }).click();

    // No `dismissMissingLabelPrompt` here: the bulk fixture's items declare no input labels, so
    // the store is created with nothing missing and that prompt never opens. Waiting for the card
    // is what tells us `POST /api/stores` finished.
    await expect(
      page.getByRole("main").getByText(STORE_DISPLAY_NAME).first(),
    ).toBeVisible({ timeout: 30_000 });
  }

  // Resolve the id through the real API rather than clicking through card DOM, matching
  // store-input-profile.real.spec.ts. The detail URL is also what the deep-link test builds on.
  const token = await readAccessToken(page);
  const response = await page.request.get("/api/stores", {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.status(), "GET /api/stores").toBe(200);
  const { items } = (await response.json()) as {
    items: { id: string; displayName: string }[];
  };
  const storeId = items.find((s) => s.displayName === STORE_DISPLAY_NAME)?.id;
  expect(storeId, `store "${STORE_DISPLAY_NAME}" must exist`).toBeTruthy();

  await page.goto(`/stores/${storeId}`);
  await expect(
    page.getByRole("heading", { name: STORE_DISPLAY_NAME }),
  ).toBeVisible();

  return page.url();
}

// ItemCard renders no heading element, so the card root is the locator that survives styling.
const cards = (page: Page) => page.locator(".item-card");
const cardTitles = (page: Page) => page.locator(".item-card .title");

test("the grid loads one slice at a time and load-more fetches the rest", async ({
  page,
}) => {
  await openBulkStore(page);

  // The first slice is a page, not the whole store: paging is real, not decorative.
  await expect(cards(page)).toHaveCount(PAGE_SIZE);

  const loadMore = page.getByRole("button", { name: "Load more" });
  await expect(loadMore).toBeVisible();
  await loadMore.click();

  await expect(cards(page)).toHaveCount(TOTAL_ITEMS);
  // Nothing left to fetch, so the affordance goes away rather than returning empty slices.
  await expect(loadMore).toHaveCount(0);
});

test("a filter narrows the grid through the server", async ({ page }) => {
  await openBulkStore(page);
  await expect(cards(page)).toHaveCount(PAGE_SIZE);

  await page.getByRole("button", { name: "Search" }).click();
  await page.getByPlaceholder("Search items...").fill("Bulk Item 007");

  // One matching item, out of sixty — and the filter was applied by the database, since the
  // browser only ever received the first 48 rows and the match had to survive a fresh query.
  await expect(cards(page)).toHaveCount(1);
  await expect(cardTitles(page)).toHaveText(["Bulk Item 007"]);
});

test("a filtered URL is shareable and reloads to the same view", async ({
  page,
}) => {
  const storeUrl = await openBulkStore(page);

  const deepLink = `${storeUrl}?cat=Badges&sort=cost&dir=desc`;
  await page.goto(deepLink);

  // Exactly the Badges third of the fixture, so the category really narrowed the query rather
  // than the page rendering everything it had.
  await expect(cards(page)).toHaveCount(BADGES_ITEMS);

  // Cost descending, applied by the server: the most expensive Badges item leads.
  const names = await cardTitles(page).allInnerTexts();
  expect(names[0]).toBe("Bulk Item 059");

  // Reloading the same URL yields the same order — the filter lives in the link.
  await page.reload();
  await expect(cardTitles(page).first()).toHaveText(names[0]);
});
