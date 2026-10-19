import { test, expect, type Page } from "@playwright/test";
import { pageOf } from "./apiEnvelopes";

const gaupointsStore = {
  id: "store-gp",
  channelId: "gaupoints",
  displayName: "GauPoints",
  isActive: true,
  lastSyncedAt: null,
  createdAt: "2026-03-01T00:00:00Z",
  missingLabels: [],
  itemCount: 12,
  avatarUrl: null,
  provider: "gaupoints",
};

const setupUserWithGauPointsSession = async (page: Page) => {
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
        locale: "en-US",
        twoFactorEnabled: false,
        credentials: [
          {
            provider: "gaupoints",
            accountName: "someviewer",
            expiresAt: "2999-01-01T00:00:00Z",
            lastValidatedAt: "2998-12-31T00:00:00Z",
            lastValidationError: null,
          },
        ],
      }),
    }),
  );
};

test("adds the GauPoints store with no username and sends only the provider", async ({
  page,
}) => {
  await setupUserWithGauPointsSession(page);

  const lookups: string[] = [];
  await page.route("**/api/stores/lookup*", (route) => {
    lookups.push(route.request().url());
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        externalId: "gaupoints",
        handle: "gaupoints",
        displayName: "GauPoints",
        avatarUrl: null,
        itemCount: 12,
      }),
    });
  });

  let created: unknown = null;
  let listed = false;
  await page.route("**/api/stores", (route) => {
    if (route.request().method() === "POST") {
      created = route.request().postDataJSON();
      listed = true;
      return route.fulfill({
        status: 201,
        contentType: "application/json",
        body: JSON.stringify(gaupointsStore),
      });
    }
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(pageOf(listed ? [gaupointsStore] : [])),
    });
  });

  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();
  await page.getByRole("button", { name: "GauPoints" }).click();

  await expect(page.getByText("Channel username")).toHaveCount(0);
  await expect(page.getByText("12 items")).toBeVisible();
  expect(lookups).toHaveLength(1);
  expect(new URL(lookups[0]).searchParams.get("provider")).toBe("gaupoints");
  expect(new URL(lookups[0]).searchParams.has("query")).toBe(false);

  await page.getByRole("button", { name: /Continue/i }).click();

  await expect.poll(() => created).toEqual({ provider: "gaupoints" });
  await expect(page.getByTestId("provider-badge")).toHaveText("GauPoints");
});

test("a StreamElements store cannot be added with only a GauPoints session", async ({
  page,
}) => {
  await setupUserWithGauPointsSession(page);
  await page.route("**/api/stores", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(pageOf([])),
    }),
  );

  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();

  await expect(
    page.getByText("Add your StreamElements credential in Settings first."),
  ).toBeVisible();
  await expect(page.getByPlaceholder("e.g. xqc")).toBeDisabled();
});
