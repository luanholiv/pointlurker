import { test, expect, type Page } from "@playwright/test";

const setupAuthWithLocale = async (page: Page, locale: string) => {
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
        locale,
      }),
    }),
  );
};

const makeRedemptionResponse = (items: object[], total = items.length) => ({
  items,
  totalCount: total,
  page: 1,
  pageSize: 20,
});

test("pre-login visitor with browser pt-BR sees Portuguese login page", async ({
  page,
}) => {
  await page.addInitScript(() => {
    Object.defineProperty(navigator, "language", {
      get: () => "pt-BR",
      configurable: true,
    });
    Object.defineProperty(navigator, "languages", {
      get: () => ["pt-BR"],
      configurable: true,
    });
  });

  await page.route("**/api/auth/bootstrap", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userExists: true }),
    }),
  );

  await page.goto("/");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: "Entrar" })).toBeVisible();
});

test("pre-login visitor with browser pt-PT falls back to en-US", async ({
  page,
}) => {
  await page.addInitScript(() => {
    Object.defineProperty(navigator, "language", {
      get: () => "pt-PT",
      configurable: true,
    });
    Object.defineProperty(navigator, "languages", {
      get: () => ["pt-PT"],
      configurable: true,
    });
  });

  await page.route("**/api/auth/bootstrap", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({ userExists: true }),
    }),
  );

  await page.goto("/");
  await page.waitForLoadState("networkidle");

  await expect(page.getByRole("button", { name: "Sign in" })).toBeVisible();
});

test("authenticated user with stored locale pt-BR sees Portuguese app shell", async ({
  page,
}) => {
  await setupAuthWithLocale(page, "pt-BR");

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(makeRedemptionResponse([])),
    }),
  );

  await page.goto("/redemptions");
  await page.waitForLoadState("networkidle");

  await expect(
    page.getByRole("heading", { level: 1, name: "Resgates" }),
  ).toBeVisible();
});

test("switching locale via Settings updates UI without full reload", async ({
  page,
}) => {
  await setupAuthWithLocale(page, "en-US");

  // After PATCH, /api/auth/me returns pt-BR locale.
  // Use a flag (not a call-count counter) so React StrictMode's double-invoke
  // of useEffect doesn't cause the pre-PATCH calls to return pt-BR.
  let localeSaved = false;
  await page.route("**/api/users/me/locale", (route) => {
    localeSaved = true;
    return route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({}),
    });
  });

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
        locale: localeSaved ? "pt-BR" : "en-US",
      }),
    }),
  );

  await page.goto("/settings");
  await page.waitForLoadState("networkidle");

  await expect(page.getByText("Language")).toBeVisible();

  await page.getByLabel(/Change language/i).selectOption("pt-BR");

  await expect(page.getByText("Idioma", { exact: true })).toBeVisible();
});

test("backend rejects unsupported locale shows localized error", async ({
  page,
}) => {
  await setupAuthWithLocale(page, "en-US");

  await page.route("**/api/users/me/locale", (route) =>
    route.fulfill({
      status: 400,
      contentType: "application/problem+json",
      body: JSON.stringify({
        title: "Locale not supported",
        status: 400,
        code: "users.locale_unsupported",
        params: { locale: "xx-XX", allowed: ["en-US", "pt-BR"] },
      }),
    }),
  );

  await page.goto("/settings");
  await page.waitForLoadState("networkidle");

  await page.getByLabel(/Change language/i).selectOption("pt-BR");

  // Asserted as one exact string, not an alternation: an alternation wide enough to admit the
  // response's own English title passes both when the copy comes from the catalog and when it leaks
  // from the backend. The code resolves to one string, and the params are interpolated into it.
  await expect(
    page.getByText("Locale 'xx-XX' is not supported."),
  ).toBeVisible();
  await expect(page.getByText("Locale not supported")).toHaveCount(0);
});

test("redemption failure history renders localized failure reason", async ({
  page,
}) => {
  await setupAuthWithLocale(page, "pt-BR");

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        makeRedemptionResponse([
          {
            id: "r-1",
            status: "SubmissionFailed",
            storeItemName: "Item X",
            storeName: "Store",
            attemptedAt: "2026-01-01T00:00:00Z",
            submittedAt: null,
            settledAt: null,
            failureContext: "401: Unauthorized",
            pollFailures: 0,
            failureCode: "redemption.failure.provider_api_unauthorized",
          },
        ]),
      ),
    }),
  );

  await page.goto("/redemptions");

  await expect(
    page.getByText("A loja não aceitou sua credencial."),
  ).toBeVisible();
});

test("redemption status badges render localized labels in pt-BR", async ({
  page,
}) => {
  await setupAuthWithLocale(page, "pt-BR");

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        makeRedemptionResponse([
          {
            id: "r-1",
            status: "Completed",
            storeItemName: "Iron Helmet",
            storeName: "Test Store",
            attemptedAt: "2026-04-14T08:00:00Z",
            submittedAt: "2026-04-14T08:01:00Z",
            settledAt: "2026-04-14T08:05:00Z",
            failureContext: null,
            pollFailures: 0,
            failureCode: null,
          },
        ]),
      ),
    }),
  );

  await page.goto("/redemptions");

  await expect(page.locator("tbody").getByText("Concluído")).toBeVisible();
});

// renderFailure() returns "—" when failureCode is null.
// There is no prose fallback — a missing catalog key requires adding the entry.
test("null failure code renders dash", async ({ page }) => {
  await setupAuthWithLocale(page, "en-US");

  await page.route("**/api/redemptions**", (route) =>
    route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        makeRedemptionResponse([
          {
            id: "r-1",
            status: "SubmissionFailed",
            storeItemName: "Mystery Item",
            storeName: "Test Store",
            attemptedAt: "2026-01-01T00:00:00Z",
            submittedAt: null,
            settledAt: null,
            failureCode: null,
            failureContext: null,
            pollFailures: 0,
          },
        ]),
      ),
    }),
  );

  await page.goto("/redemptions");

  await expect(page.getByText("Mystery Item")).toBeVisible();
  await expect(
    page.locator("tbody td").filter({ hasText: /^—$/ }).first(),
  ).toBeVisible();
});
