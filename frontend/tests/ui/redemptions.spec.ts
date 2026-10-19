import { test, expect, type Page } from "@playwright/test";
import { stubRedemptions } from "./apiEnvelopes";

const setupAuth = async (page: Page) => {
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
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userExists: true }),
    }),
  );
  await page.route("**/api/users/me", (route) =>
    route.fulfill({
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
    }),
  );
};

const makeRedemptionResponse = (items: object[], total = items.length) => ({
  items,
  totalCount: total,
  offset: 0,
  limit: 20,
});

const pendingRedemption = {
  id: "r-1",
  status: "Pending",
  storeItemName: "Golden Sword",
  storeName: "Test Store",
  attemptedAt: "2026-04-15T10:00:00Z",
  submittedAt: null,
  settledAt: null,
  failureCode: null,
  failureContext: null,
  pollFailures: 0,
};

const submittedRedemption = {
  id: "r-2",
  status: "Submitted",
  storeItemName: "Silver Shield",
  storeName: "Test Store",
  attemptedAt: "2026-04-15T09:00:00Z",
  submittedAt: "2026-04-15T09:01:00Z",
  settledAt: null,
  failureCode: null,
  failureContext: null,
  pollFailures: 0,
};

const completedRedemption = {
  id: "r-3",
  status: "Completed",
  storeItemName: "Iron Helmet",
  storeName: "Test Store",
  attemptedAt: "2026-04-14T08:00:00Z",
  submittedAt: "2026-04-14T08:01:00Z",
  settledAt: "2026-04-14T08:05:00Z",
  failureCode: null,
  failureContext: null,
  pollFailures: 0,
};

const failedRedemption = {
  id: "r-4",
  status: "SubmissionFailed",
  storeItemName: "Magic Staff",
  storeName: "Test Store",
  attemptedAt: "2026-04-13T07:00:00Z",
  submittedAt: null,
  settledAt: null,
  failureCode: "redemption.failure.provider_api_unauthorized",
  failureContext: "HTTP 401: Unauthorized",
  pollFailures: 0,
};

const timedOutRedemption = {
  id: "r-5",
  status: "CompletionTimedOut",
  storeItemName: "Dragon Armor",
  storeName: "Test Store",
  attemptedAt: "2026-04-12T06:00:00Z",
  submittedAt: "2026-04-12T06:01:00Z",
  settledAt: null,
  failureCode: null,
  failureContext: null,
  pollFailures: 0,
};

test("full lifecycle — pending → submitted → completed shown in history", async ({
  page,
}) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        makeRedemptionResponse([
          completedRedemption,
          submittedRedemption,
          pendingRedemption,
        ]),
      ),
    }),
  );

  await page.goto("/redemptions");

  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Silver Shield")).toBeVisible();
  await expect(page.getByText("Iron Helmet")).toBeVisible();

  const badges = page
    .locator("span")
    .filter({ hasText: /^(Pending|Submitted|Completed|Success)$/ });
  await expect(badges.first()).toBeVisible();
});

test("submission failure — SubmissionFailed status and failure reason displayed", async ({
  page,
}) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([failedRedemption])),
    }),
  );

  await page.goto("/redemptions");

  await expect(page.getByText("Magic Staff")).toBeVisible();
  await expect(page.locator("tbody").getByText("Failed")).toBeVisible();
  await expect(
    page.getByText("The store did not accept your credential."),
  ).toBeVisible();
});

test("timeout — CompletionTimedOut badge displayed", async ({ page }) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([timedOutRedemption])),
    }),
  );

  await page.goto("/redemptions");

  await expect(page.getByText("Dragon Armor")).toBeVisible();
  await expect(page.locator("tbody").getByText("Timed Out")).toBeVisible();
});

const rejectedRedemption = {
  id: "r-6",
  status: "Rejected",
  storeItemName: "Refused Cape",
  storeName: "Test Store",
  attemptedAt: "2026-04-11T05:00:00Z",
  submittedAt: "2026-04-11T05:01:00Z",
  settledAt: "2026-04-11T05:30:00Z",
  failureCode: null,
  failureContext: null,
  pollFailures: 0,
};

const wedgedRedemption = {
  id: "r-7",
  status: "Submitted",
  storeItemName: "Wedged Totem",
  storeName: "Test Store",
  attemptedAt: "2026-04-10T04:00:00Z",
  submittedAt: "2026-04-10T04:01:00Z",
  settledAt: null,
  failureCode: null,
  failureContext: null,
  pollFailures: 12,
};

// A refusal upstream is its own end state, not a timeout — the outcome is known, not missing.
test("rejection — Rejected badge displayed", async ({ page }) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([rejectedRedemption])),
    }),
  );

  await page.goto("/redemptions");

  await expect(page.getByText("Refused Cape")).toBeVisible();
  await expect(page.locator("tbody").getByText("Rejected")).toBeVisible();
});

// A submitted row whose completion cannot be read has no failure code, so without a signal of its
// own it is indistinguishable from one still waiting upstream.
test("wedged row — a failing status check is reported against the row", async ({
  page,
}) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([wedgedRedemption])),
    }),
  );

  await page.goto("/redemptions");

  await expect(page.getByText("Wedged Totem")).toBeVisible();
  await expect(
    page.getByText("Status check failing (12 attempts)"),
  ).toBeVisible();
});

// Submitted is the state a redemption sticks in when its completion cannot be read, so it needs a
// chip of its own — without one those rows cannot be listed at all.
test("filters — Submitted chip sends its own status param", async ({
  page,
}) => {
  await setupAuth(page);

  let capturedUrl = "";
  await page.route("**/api/redemptions**", (route) => {
    capturedUrl = route.request().url();
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([wedgedRedemption])),
    });
  });

  await page.goto("/redemptions");
  await page.waitForLoadState("networkidle");

  await page.getByRole("button", { name: "Search" }).click();
  await page.getByRole("button", { name: "Submitted", exact: true }).click();
  await page.waitForTimeout(200);

  expect(capturedUrl).toContain("status=Submitted");
});

test("filters — status chip filter sends correct query param", async ({
  page,
}) => {
  await setupAuth(page);

  let capturedUrl = "";

  await page.route("**/api/redemptions**", (route) => {
    capturedUrl = route.request().url();
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([completedRedemption])),
    });
  });

  await page.goto("/redemptions");
  await page.waitForLoadState("networkidle");

  // Open filter panel then apply status filter
  await page.getByRole("button", { name: "Search" }).click();
  await page.getByRole("button", { name: "Success" }).click();
  await page.waitForTimeout(200);

  expect(capturedUrl).toContain("status=Completed");
});

test("retry — completed redemption visible after transient error then success", async ({
  page,
}) => {
  await setupAuth(page);

  // Always error initially (React StrictMode double-invokes effects; both runs must fail for error state)
  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({ status: 503, contentType: "application/json", body: "{}" }),
  );

  await page.goto("/redemptions");

  // Error state shows retry button
  await expect(page.getByRole("button", { name: "Retry" })).toBeVisible();

  // Override route to return success, then retry
  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([completedRedemption])),
    }),
  );

  await page.getByRole("button", { name: "Retry" }).click();
  await expect(page.getByText("Iron Helmet")).toBeVisible();
});

test("history page — renders table and status chip filter updates results", async ({
  page,
}) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) => {
    const url = new URL(route.request().url());
    const status = url.searchParams.get("status");

    const items = [
      completedRedemption,
      failedRedemption,
      pendingRedemption,
    ].filter((r) => !status || r.status === status);
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse(items)),
    });
  });

  await page.goto("/redemptions");

  // All items visible initially
  await expect(page.getByText("Golden Sword")).toBeVisible();
  await expect(page.getByText("Magic Staff")).toBeVisible();
  await expect(page.getByText("Iron Helmet")).toBeVisible();

  // Open filter panel then filter to Completed only via "Success" chip
  await page.getByRole("button", { name: "Search" }).click();
  await page.getByRole("button", { name: "Success" }).click();
  await expect(page.getByText("Iron Helmet")).toBeVisible();
  await expect(page.getByText("Golden Sword")).not.toBeVisible();
  await expect(page.getByText("Magic Staff")).not.toBeVisible();
});

test("sorting — click column header sorts items", async ({ page }) => {
  await setupAuth(page);

  // The stub orders from the query string, so these assertions now also prove the click became a
  // request rather than a re-arrangement of the rows already on screen.
  await stubRedemptions(page, [
    pendingRedemption,
    completedRedemption,
    submittedRedemption,
  ]);

  await page.goto("/redemptions");
  await expect(page.getByText("Golden Sword")).toBeVisible();

  // Sort by Item name ascending
  await page.getByRole("columnheader", { name: /Item/ }).click();

  const rows = page.locator("tbody tr");
  await expect(rows.first()).toContainText("Golden Sword");

  // Sort descending on second click
  await page.getByRole("columnheader", { name: /Item/ }).click();
  await expect(rows.first()).toContainText("Silver Shield");
});

test("pagination — previous/next buttons navigate pages", async ({ page }) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) => {
    const url = new URL(route.request().url());
    const offset = Number(url.searchParams.get("offset") ?? 0);
    const item = offset === 0 ? completedRedemption : failedRedemption;
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        items: [item],
        totalCount: 40,
        offset,
        limit: 20,
      }),
    });
  });

  await page.goto("/redemptions");
  await expect(page.getByText("Iron Helmet")).toBeVisible();
  await expect(page.getByText("1 / 2")).toBeVisible();

  await page.getByRole("button", { name: "Next" }).click();
  await expect(page.getByText("Magic Staff")).toBeVisible();
  await expect(page.getByText("2 / 2")).toBeVisible();

  // Previous disabled check: go back
  await page.getByRole("button", { name: "Previous" }).click();
  await expect(page.getByText("Iron Helmet")).toBeVisible();
});

/**
 * Sorting reorders the whole history, not the twenty rows the page is holding. Re-arranging the
 * loaded rows and presenting that as a sorted history is simply wrong on any history longer than a
 * page.
 *
 * Sorting from page 2 is what makes the two behaviours distinguishable. The row this asserts on
 * ranks first across all 25 and was never on page 2, so a client-side re-arrangement cannot produce
 * it; only a request that re-ordered the whole history can.
 */
test("sorting from a later page reorders the whole history, not the page", async ({
  page,
}) => {
  await setupAuth(page);

  // 25 rows over a 20-row page. Names run Z..B as the id runs up, so ordering by item name is the
  // reverse of the order the pager walks — the first name belongs to the last row.
  const history = Array.from({ length: 25 }, (_, i) => ({
    ...completedRedemption,
    id: `r-${String(i + 1).padStart(2, "0")}`,
    storeItemName: `Item ${String.fromCharCode(90 - i)}`,
    attemptedAt: `2026-04-${String(i + 1).padStart(2, "0")}T08:00:00Z`,
  }));

  await stubRedemptions(page, history);

  await page.goto("/redemptions");
  await expect(page.getByText("1 / 2")).toBeVisible();

  await page.getByRole("button", { name: "Next" }).click();
  await expect(page.getByText("2 / 2")).toBeVisible();
  // Page 2 holds the tail of the default order, so the alphabetically-first name is not on it.
  await expect(page.getByText("Item B", { exact: true })).toHaveCount(0);

  await page.getByRole("columnheader", { name: /Item/ }).click();

  // First across all 25 by name — a row page 2 never held.
  await expect(page.locator("tbody tr").first()).toContainText("Item B");
});

test("empty state — shows no redemptions message", async ({ page }) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([])),
    }),
  );

  await page.goto("/redemptions");
  await expect(
    page.getByRole("heading", { name: "No redemptions yet" }),
  ).toBeVisible();
});

test("redemptions page — renders heading and empty state", async ({ page }) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([])),
    }),
  );

  await page.goto("/redemptions");
  await expect(
    page.getByRole("heading", { level: 1, name: "Redemptions" }),
  ).toBeVisible();
});

test("sidebar — Redemptions nav link navigates to /redemptions", async ({
  page,
}) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([])),
    }),
  );

  await page.goto("/");
  await page.getByRole("link", { name: /Redemptions/i }).click();
  await expect(page).toHaveURL(/\/redemptions/);
  await expect(
    page.getByRole("heading", { level: 1, name: "Redemptions" }),
  ).toBeVisible();
});

test("Search button shows and hides filter panel", async ({ page }) => {
  await setupAuth(page);

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([])),
    }),
  );

  await page.goto("/redemptions");

  // Filter panel hidden by default
  await expect(page.getByRole("button", { name: "All" })).not.toBeVisible();
  await expect(page.getByRole("button", { name: "Success" })).not.toBeVisible();

  await page.getByRole("button", { name: "Search" }).click();
  await expect(page.getByRole("button", { name: "All" })).toBeVisible();
  await expect(page.getByRole("button", { name: "Success" })).toBeVisible();
  await expect(page.getByLabel("From date")).toBeVisible();
  await expect(page.getByLabel("To date")).toBeVisible();

  await page.getByRole("button", { name: "Search" }).click();
  await expect(page.getByRole("button", { name: "All" })).not.toBeVisible();
});
