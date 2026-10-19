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

const baseStoreItem = {
  id: "item-1",
  providerItemId: "se1",
  name: "VIP Badge",
  description: null as string | null,
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

type UnresolvedInput = {
  rawLabel: string;
  bestCanonical: string | null;
  bestScore: number;
  aliasTarget: string | null;
  itemCount: number;
  blockedEntryCount: number;
};

type ResolvedInput = {
  labelCanonical: string;
  updatedAt: string;
  requestedBy: {
    rawLabel: string;
    source: "Exact" | "Alias" | "Fuzzy";
    confidence: number;
    itemCount: number;
  }[];
};

type StoreState = {
  storeMissingLabels: string[];
  unresolved: UnresolvedInput[];
  resolved: ResolvedInput[];
  canonicalLabels: string[];
  profileGets: number;
};

const defaultState = (): StoreState => ({
  storeMissingLabels: [],
  unresolved: [],
  resolved: [],
  canonicalLabels: [],
  profileGets: 0,
});

const unresolvedInput = (
  over: Partial<UnresolvedInput> & { rawLabel: string },
): UnresolvedInput => ({
  bestCanonical: null,
  bestScore: 0,
  aliasTarget: null,
  itemCount: 1,
  blockedEntryCount: 0,
  ...over,
});

const setupStoreRoutes = async (page: Page, state: StoreState) => {
  // The grid fetches its own slice now; the store envelope only carries the header's counts.
  await stubStoreItems(page, "store-1", [baseStoreItem]);

  await page.route("**/api/stores/store-1", async (route) => {
    if (route.request().method() !== "GET") return route.fallback();
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: "store-1",
        channelId: "ch1",
        displayName: "Test Store",
        isActive: true,
        lastSyncedAt: "2026-05-01T00:00:00Z",
        createdAt: "2026-03-01T00:00:00Z",
        itemCount: 1,
        missingLabels: state.storeMissingLabels,
      }),
    });
  });

  await page.route("**/api/stores/store-1/profile", async (route) => {
    if (route.request().method() !== "GET") return route.fallback();
    state.profileGets++;
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        unresolved: state.unresolved,
        resolved: state.resolved,
        canonicalLabels: state.canonicalLabels,
      }),
    });
  });
};

const itemsTab = (page: Page) => page.getByRole("tab", { name: /items/i });
const profileTab = (page: Page) => page.getByRole("tab", { name: /profile/i });
const dot = (page: Page) => page.getByTestId("notification-dot");

test("7.1 store detail defaults to Items tab — grid visible, profile editor absent", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreRoutes(page, defaultState());

  await page.goto("/stores/store-1");
  await expect(itemsTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(page.getByText("VIP Badge")).toBeVisible();
  await expect(page.getByTestId("store-profile-editor")).toHaveCount(0);
});

test("7.2 clicking Profile tab shows config and sets #profile; Items removes hash", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  const state = defaultState();
  state.unresolved = [
    unresolvedInput({
      rawLabel: "twitchh usrname",
      bestCanonical: "Twitch Username",
      bestScore: 60,
    }),
  ];
  state.canonicalLabels = ["Twitch Username"];
  await setupStoreRoutes(page, state);

  await page.goto("/stores/store-1");
  await profileTab(page).click();
  await expect(page.getByTestId("store-profile-editor")).toBeVisible();
  // Unresolved labels live inside the single editor panel (no separate section).
  await expect(page.getByTestId("store-alias-confirmation")).toHaveCount(0);
  await expect(page.getByTestId("profile-row-twitchh usrname")).toBeVisible();
  expect(new URL(page.url()).hash).toBe("#profile");

  await itemsTab(page).click();
  await expect(page.getByText("VIP Badge")).toBeVisible();
  await expect(page.getByTestId("store-profile-editor")).toHaveCount(0);
  expect(new URL(page.url()).hash).toBe("");
});

test("7.3 deep link /stores/{id}#profile opens Profile tab", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreRoutes(page, defaultState());

  await page.goto("/stores/store-1#profile");
  await expect(profileTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(page.getByTestId("store-profile-editor")).toBeVisible();
});

test("7.4 full page reload on #profile restores Profile tab", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await setupStoreRoutes(page, defaultState());

  await page.goto("/stores/store-1#profile");
  await expect(page.getByTestId("store-profile-editor")).toBeVisible();

  await page.reload();
  await expect(profileTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(page.getByTestId("store-profile-editor")).toBeVisible();
});

test("7.5 unknown hash falls back to Items tab", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await setupStoreRoutes(page, defaultState());

  await page.goto("/stores/store-1#something-else");
  await expect(itemsTab(page)).toHaveAttribute("aria-selected", "true");
  await expect(page.getByText("VIP Badge")).toBeVisible();
});

test("7.6 dot visible for missing labels, with sr-only text", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  const state = defaultState();
  state.storeMissingLabels = ["Discord Tag"];
  state.unresolved = [unresolvedInput({ rawLabel: "Discord Tag" })];
  await setupStoreRoutes(page, state);

  await page.goto("/stores/store-1");
  await expect(dot(page)).toBeAttached();
  await expect(profileTab(page).locator(".sr-only")).toHaveText(
    "Profile needs attention",
  );
});

test("7.7 dot visible when only unresolved aliases exist", async ({ page }) => {
  await setupAuthenticatedUser(page);
  const state = defaultState();
  state.unresolved = [
    unresolvedInput({
      rawLabel: "twitchh usrname",
      bestCanonical: "Twitch Username",
      bestScore: 60,
    }),
  ];
  state.canonicalLabels = ["Twitch Username"];
  await setupStoreRoutes(page, state);

  await page.goto("/stores/store-1");
  await expect(dot(page)).toBeAttached();
});

test("7.8 no dot when configuration is complete", async ({ page }) => {
  await setupAuthenticatedUser(page);
  const state = defaultState();
  await setupStoreRoutes(page, state);

  await page.goto("/stores/store-1");
  await expect(page.getByText("VIP Badge")).toBeVisible();
  await expect.poll(() => state.profileGets).toBeGreaterThan(0);
  await expect(dot(page)).toHaveCount(0);
});

test("7.9 filling missing values clears dot without page refresh", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  const state = defaultState();
  state.unresolved = [unresolvedInput({ rawLabel: "Discord Tag" })];
  await setupStoreRoutes(page, state);

  await page.route("**/api/stores/store-1/profile/values", async (route) => {
    if (route.request().method() === "PUT") {
      state.resolved = [
        {
          labelCanonical: "Discord Tag",
          updatedAt: "2026-05-07T00:00:00Z",
          requestedBy: [
            {
              rawLabel: "Discord Tag",
              source: "Exact",
              confidence: 100,
              itemCount: 1,
            },
          ],
        },
      ];
      state.canonicalLabels = ["Discord Tag"];
      state.unresolved = [];
      await route.fulfill({ status: 204 });
      return;
    }
    return route.fallback();
  });

  await page.goto("/stores/store-1#profile");
  await expect(dot(page)).toBeAttached();

  await page.getByTestId("profile-value-Discord Tag").fill("xqc#1234");
  await page.getByRole("button", { name: /Save profile/i }).click();

  await expect(dot(page)).toHaveCount(0);
});

test("7.10 resolving last alias clears dot without page refresh", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  const state = defaultState();
  state.unresolved = [
    unresolvedInput({
      rawLabel: "twitchh usrname",
      bestCanonical: "Twitch Username",
      bestScore: 60,
    }),
  ];
  state.canonicalLabels = ["Twitch Username"];
  await setupStoreRoutes(page, state);

  await page.route("**/api/stores/store-1/profile/aliases", async (route) => {
    if (route.request().method() === "POST") {
      state.unresolved = [];
      await route.fulfill({ status: 204 });
      return;
    }
    return route.fallback();
  });

  await page.goto("/stores/store-1#profile");
  await expect(dot(page)).toBeAttached();

  // Switching to "Map to existing" prefills the picker with the server's best match
  // ("Twitch Username"); a single Save resolves it and clears the dot without a refresh.
  await page.getByTestId("profile-mode-alias-twitchh usrname").click();
  await expect(
    page.getByTestId("profile-canonical-twitchh usrname"),
  ).toHaveValue("Twitch Username");
  await page.getByRole("button", { name: /Save profile/i }).click();

  await expect(dot(page)).toHaveCount(0);
});

test("7.11 profile endpoint fetched once per mount, no refetch loop", async ({
  page,
}) => {
  // One logical fetch, no N+1 / refetch loop. The dev server runs under React
  // StrictMode, which intentionally double-invokes effects in development (production
  // fetches once); the unit test asserts the exact single call. Here we assert the count
  // settles and never grows — i.e. no fetch loop — within the StrictMode dev ceiling.
  await setupAuthenticatedUser(page);
  const state = defaultState();
  await setupStoreRoutes(page, state);

  await page.goto("/stores/store-1");
  await expect(page.getByText("VIP Badge")).toBeVisible();
  await expect.poll(() => state.profileGets).toBeGreaterThanOrEqual(1);

  // settle, then capture; the count must not grow afterward (no refetch loop)
  await page.waitForTimeout(500);
  const profileAfterSettle = state.profileGets;
  expect(profileAfterSettle).toBeLessThanOrEqual(2);

  await page.waitForTimeout(500);
  expect(state.profileGets).toBe(profileAfterSettle);
});
