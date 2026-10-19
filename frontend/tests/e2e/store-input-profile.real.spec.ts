import { test, expect, type Page } from "@playwright/test";
import {
  dismissMissingLabelPrompt,
  ensureSeToken,
  forbidPageRoute,
  loginAsAdmin,
  readAccessToken,
  storeIsListed,
} from "./realStack";

// Real, UNMOCKED coverage of the store input profile tab. Proves the things a route mock
// structurally cannot:
//
//   - the REAL resolver emits Exact / Alias for real fixture labels (a route mock hardcodes
//     "source": "Exact", so it can never show the backend actually produces it);
//   - a label with spaces, slashes and parentheses survives browser → nginx → Kestrel →
//     query-string binding on DELETE;
//   - the delete is genuinely idempotent (a second DELETE is 204, not 404);
//   - the unresolved list arrives sorted by the server, not by the component.
//
// This spec owns its OWN store: `channels-lookup-profile.json` gives username `e2e_profile`
// a distinct channel `_id`, so the destructive steps below cannot disturb the store that
// smoke.real.spec.ts creates.
//
// Re-entrant by design (one retry, no DB reset between attempts): every step is
// check-or-create, and the ordering assertion is computed from the rendered set rather than
// hardcoded, so it holds against a partially-configured store on a retry.

const SE_USERNAME = "e2e_profile";
// Must match frontend/tests/e2e/wiremock/__files/channel-response-profile.json displayName.
const STORE_DISPLAY_NAME = "E2E Profile Store";

// The four distinct user-input labels in the shared StreamElements fixture
// (backend/tests/.../Fixtures/store-items-response.json).
const NICK_TWITCH = "Nick Twitch";
const EMAIL_LABEL = "E-mail";
const TRADE_LINK = "Steam Trade Link";
// Spaces AND parentheses: the natural probe for the query-string encoding risk.
const PARENTHESISED = "Link Steam (Deixe o perfil desbloqueado)";

test.beforeEach(async ({ page }) => {
  test.setTimeout(180_000);
  forbidPageRoute(page, "store-input-profile.real.spec.ts");
});

/** Creates the spec's own store if it is not there yet, and returns its id. */
async function ensureProfileStore(page: Page): Promise<string> {
  if (!(await storeIsListed(page, STORE_DISPLAY_NAME))) {
    await page.getByRole("button", { name: "Add Store" }).first().click();
    await page.getByPlaceholder("e.g. xqc").fill(SE_USERNAME);
    await page.getByRole("button", { name: "Lookup" }).click();
    await expect(
      page.getByRole("dialog").getByText(STORE_DISPLAY_NAME),
    ).toBeVisible();
    await page.getByRole("button", { name: /Continue/i }).click();
    await dismissMissingLabelPrompt(page);
  }

  await expect(
    page.getByRole("main").getByText(STORE_DISPLAY_NAME).first(),
  ).toBeVisible();

  // Resolve the id through the real API rather than guessing at card DOM structure — the
  // same nginx → backend path the app uses, nothing stubbed.
  let storeId: string | undefined;
  await expect
    .poll(
      async () => {
        const stores = await apiJson<{
          items: { id: string; displayName: string }[];
        }>(page, "/api/stores");
        storeId = stores.items.find(
          (s) => s.displayName === STORE_DISPLAY_NAME,
        )?.id;
        return storeId;
      },
      { message: `store "${STORE_DISPLAY_NAME}" must exist` },
    )
    .toBeTruthy();
  return storeId!;
}

async function apiJson<T>(page: Page, path: string): Promise<T> {
  const token = await readAccessToken(page);
  const response = await page.request.get(path, {
    headers: { Authorization: `Bearer ${token}` },
  });
  expect(response.status(), `GET ${path}`).toBe(200);
  return (await response.json()) as T;
}

const openProfile = async (page: Page, storeId: string) => {
  await page.goto(`/stores/${storeId}#profile`);
  await expect(page.getByTestId("store-profile-editor")).toBeVisible();
};

/** Sets a value for a still-unresolved raw label. No-op when it already resolves. */
async function ensureValue(page: Page, rawLabel: string, value: string) {
  const input = page.getByTestId(`profile-value-${rawLabel}`);
  if (!(await input.isVisible())) return;
  await input.fill(value);
  await page.getByRole("button", { name: /Save profile/i }).click();
  await expect(page.getByTestId(`profile-status-${rawLabel}`)).toContainText(
    /resolved/i,
  );
}

test("profile tab reflects the real resolver end to end", async ({ page }) => {
  await loginAsAdmin(page);
  await ensureSeToken(page);
  const storeId = await ensureProfileStore(page);
  await openProfile(page, storeId);

  // R1.1 — every raw label the fixture's items request is on the page somewhere, whether it
  // resolves or not. Without this the ordering check below could pass on an empty list.
  for (const label of [NICK_TWITCH, EMAIL_LABEL, TRADE_LINK, PARENTHESISED]) {
    await expect(
      page
        .getByTestId(`profile-row-${label}`)
        .or(page.getByTestId(`profile-requester-${label}`))
        .first(),
      `"${label}" must appear on the profile tab`,
    ).toBeVisible();
  }

  // R1.7 — the server sorts. All four fixture labels tie on item and blocked counts, so the
  // rule reduces to the label; assert the RENDERED set is in that order (re-entrant: a retry
  // sees fewer unresolved rows, and a subset of a sorted list is still sorted).
  const renderedUnresolved = await page
    .locator('[data-testid^="profile-mode-value-"]')
    .evaluateAll((nodes) =>
      nodes.map((n) =>
        (n.getAttribute("data-testid") ?? "").replace(
          "profile-mode-value-",
          "",
        ),
      ),
    );
  const ordinalIgnoreCase = (a: string, b: string) => {
    const [x, y] = [a.toUpperCase(), b.toUpperCase()];
    return x < y ? -1 : x > y ? 1 : 0;
  };
  expect(renderedUnresolved).toEqual(
    [...renderedUnresolved].sort(ordinalIgnoreCase),
  );

  // R2.* — an exactly-matching definition must come back tagged `exact` by the REAL resolver.
  await ensureValue(page, NICK_TWITCH, "e2e_viewer");
  await expect(
    page.getByTestId(`profile-requester-${NICK_TWITCH}`),
  ).toContainText(/exact/i);

  // R2.* — and a manual mapping must come back tagged `alias`.
  await ensureValue(page, EMAIL_LABEL, "viewer@e2e.test");
  const tradeLinkChip = page.getByTestId(`profile-requester-${TRADE_LINK}`);
  if (!(await tradeLinkChip.isVisible())) {
    await page.getByTestId(`profile-mode-alias-${TRADE_LINK}`).click();
    await page
      .getByTestId(`profile-canonical-${TRADE_LINK}`)
      .selectOption(EMAIL_LABEL);
    await page.getByRole("button", { name: /Save profile/i }).click();
  }
  await expect(tradeLinkChip).toContainText(/alias/i);
  // The alias chip is the only detachable one — `exact` is derived, not user-authored.
  await expect(page.getByTestId(`profile-detach-${TRADE_LINK}`)).toBeVisible();
  await expect(page.getByTestId(`profile-detach-${NICK_TWITCH}`)).toHaveCount(
    0,
  );
});

test("a label needing url encoding survives nginx and deletes idempotently", async ({
  page,
}) => {
  await loginAsAdmin(page);
  await ensureSeToken(page);
  const storeId = await ensureProfileStore(page);
  await openProfile(page, storeId);

  // Give the parenthesised label a value so there is a slot to delete.
  await ensureValue(page, PARENTHESISED, "https://steamcommunity.com/id/e2e");

  // R3.3 — delete through the UI. The label travels in the query string, so this is the
  // assertion that browser → nginx → Kestrel → route binding round-trips it intact.
  await page.getByTestId(`profile-delete-${PARENTHESISED}`).click();
  await page.getByTestId(`profile-delete-confirm-${PARENTHESISED}`).click();

  // The slot is gone; the raw label goes back to needing a value.
  await expect(
    page.getByTestId(`profile-status-${PARENTHESISED}`),
  ).toContainText(/needs value/i);
  await expect(
    page.getByTestId(`profile-value-${PARENTHESISED}`),
  ).toBeVisible();

  // R3.2 — repeat the delete against the real backend and assert the status code itself.
  // The UI cannot express this (the row is gone), and 204-not-404 is the whole contract.
  const token = await readAccessToken(page);
  const repeat = await page.request.delete(
    `/api/stores/${storeId}/profile/values?label=${encodeURIComponent(PARENTHESISED)}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  expect(repeat.status()).toBe(204);

  // Same for a never-existing alias: absent is not an error.
  const absentAlias = await page.request.delete(
    `/api/stores/${storeId}/profile/aliases?rawLabel=${encodeURIComponent("never mapped / at all")}`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  expect(absentAlias.status()).toBe(204);
});
