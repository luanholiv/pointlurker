import path from "node:path";
import { test, expect, type Page, type TestInfo } from "@playwright/test";
import {
  fulfillJson,
  pageOf,
  stubRedemptions,
  stubStoreSummary,
} from "./apiEnvelopes";

/**
 * Full-page captures of every main surface, in both themes, for reviewing typography against
 * docs/DESIGN.md. Each capture is attached to the HTML report; set DESIGN_SCREENSHOT_DIR to also
 * write them to a folder. The assertions only prove the page rendered its data — the images are
 * what a reviewer reads.
 */

const stores = [
  {
    id: "store-1",
    channelId: "ch-1",
    displayName: "Gaules",
    isActive: true,
    lastSyncedAt: "2026-09-30T18:42:00Z",
    itemCount: 128,
    avatarUrl: null,
    provider: "streamelements",
  },
  {
    id: "store-2",
    channelId: "gaupoints",
    displayName: "GauPoints",
    isActive: true,
    lastSyncedAt: "2026-09-30T18:40:00Z",
    itemCount: 42,
    avatarUrl: null,
    provider: "gaupoints",
  },
  {
    id: "store-3",
    channelId: "ch-3",
    displayName: "Loud Coringa",
    isActive: false,
    lastSyncedAt: "2026-09-12T09:05:00Z",
    itemCount: 17,
    avatarUrl: null,
    provider: "streamelements",
  },
];

const entry = (
  id: string,
  itemName: string,
  itemCost: number,
  extra: Record<string, unknown> = {},
) => ({
  id,
  storeItemId: `item-${id}`,
  itemName,
  itemCost,
  isAvailable: true,
  notifyOnAvailable: true,
  autoRedeem: false,
  desiredRedemptions: 1,
  quotaUsed: 0,
  isBlocked: false,
  missingLabels: [],
  createdAt: "2026-09-01T12:00:00Z",
  thumbnailUrl: null,
  ...extra,
});

const wishlistGroups = [
  {
    storeId: "store-1",
    storeName: "Gaules",
    isActive: true,
    lastSyncedAt: "2026-09-30T18:42:00Z",
    avatarUrl: null,
    entries: [
      entry("w1", "Gift card Steam R$ 50", 25000, {
        autoRedeem: true,
        desiredRedemptions: 3,
        quotaUsed: 1,
      }),
      entry("w2", "Skin AK-47 Redline", 80000, { isAvailable: false }),
      entry("w3", "Camiseta oficial", 40000, {
        autoRedeem: true,
        isBlocked: true,
        missingLabels: ["Tamanho", "Endereço"],
      }),
    ],
  },
  {
    storeId: "store-2",
    storeName: "GauPoints",
    isActive: true,
    lastSyncedAt: "2026-09-30T18:40:00Z",
    avatarUrl: null,
    entries: [entry("w4", "Vale-presente iFood R$ 30", 1200)],
  },
];

const redemption = (
  id: string,
  status: string,
  storeItemName: string,
  storeName: string,
  attemptedAt: string,
  extra: Record<string, unknown> = {},
) => ({
  id,
  status,
  storeItemName,
  storeName,
  attemptedAt,
  submittedAt: attemptedAt,
  settledAt: status === "Completed" ? attemptedAt : null,
  failureCode: null,
  failureContext: null,
  pollFailures: 0,
  provider: storeName === "GauPoints" ? "gaupoints" : "streamelements",
  ...extra,
});

const redemptions = [
  redemption(
    "r1",
    "Completed",
    "Gift card Steam R$ 50",
    "Gaules",
    "2026-09-29T21:10:00Z",
  ),
  redemption(
    "r2",
    "Submitted",
    "Vale-presente iFood R$ 30",
    "GauPoints",
    "2026-09-30T08:00:00Z",
  ),
  redemption(
    "r3",
    "SubmissionFailed",
    "Skin AK-47 Redline",
    "Gaules",
    "2026-09-28T14:22:00Z",
    {
      submittedAt: null,
      failureCode: "redemption.failure.provider_api_unauthorized",
    },
  ),
  redemption(
    "r4",
    "Pending",
    "Camiseta oficial",
    "Gaules",
    "2026-09-30T18:45:00Z",
    {
      submittedAt: null,
    },
  ),
];

const stubApi = async (page: Page) => {
  await page.route("**/api/auth/bootstrap", (route) =>
    fulfillJson(route, { userExists: true }),
  );
  await page.route("**/api/users/me", (route) =>
    fulfillJson(route, {
      userId: "4f1c2a9e-7b3d-4e8a-9c21-0d5e6f7a8b9c",
      email: "luan.oliveira@example.com.br",
      locale: "pt-BR",
      twoFactorEnabled: false,
      credentials: [
        {
          provider: "streamelements",
          accountName: "luanzinho",
          expiresAt: null,
          lastValidatedAt: "2026-09-30T18:00:00Z",
          lastValidationError: null,
        },
        {
          provider: "gaupoints",
          accountName: "luanzinho",
          expiresAt: "2026-10-06T18:00:00Z",
          lastValidatedAt: "2026-09-30T18:00:00Z",
          lastValidationError: null,
        },
      ],
    }),
  );
  await page.route("**/api/stores**", (route) => {
    const url = new URL(route.request().url());
    if (url.pathname.endsWith("/api/stores/summary")) return route.fallback();
    return fulfillJson(route, pageOf(stores));
  });
  await stubStoreSummary(page, stores);
  await page.route("**/api/wishlist**", (route) =>
    fulfillJson(route, pageOf(wishlistGroups)),
  );
  await stubRedemptions(page, redemptions);
};

const views: {
  name: string;
  url: string;
  ready: (page: Page) => Promise<void>;
}[] = [
  {
    name: "stores",
    url: "/stores",
    ready: (page) => expect(page.getByText("Loud Coringa")).toBeVisible(),
  },
  {
    name: "wishlist",
    url: "/wishlist",
    ready: (page) => expect(page.getByText("Skin AK-47 Redline")).toBeVisible(),
  },
  {
    name: "redemptions",
    url: "/redemptions",
    ready: (page) => expect(page.getByText("Camiseta oficial")).toBeVisible(),
  },
  {
    name: "settings-account",
    url: "/settings",
    ready: async (page) => {
      await expect(
        page.getByRole("heading", { level: 2, name: "Identidade" }),
      ).toBeVisible();
      await expect(
        page.getByText("luan.oliveira@example.com.br"),
      ).toBeVisible();
    },
  },
  {
    name: "settings-credentials",
    url: "/settings#token",
    ready: async (page) => {
      await expect(
        page.getByRole("heading", { level: 2, name: "StreamElements" }),
      ).toBeVisible();
      await expect(
        page.getByRole("heading", { level: 2, name: "GauPoints" }),
      ).toBeVisible();
    },
  },
  {
    name: "settings-security",
    url: "/settings#security",
    ready: (page) =>
      expect(
        page.getByRole("heading", { level: 2, name: "Alterar senha" }),
      ).toBeVisible(),
  },
];

const capture = async (page: Page, testInfo: TestInfo, fileName: string) => {
  // Finite animations are fast-forwarded so a card caught mid-entrance is not half transparent.
  const image = await page.screenshot({
    fullPage: true,
    animations: "disabled",
  });
  await testInfo.attach(fileName, { body: image, contentType: "image/png" });

  const dir = process.env.DESIGN_SCREENSHOT_DIR;
  if (dir) {
    const fs = await import("node:fs/promises");
    await fs.mkdir(dir, { recursive: true });
    await fs.writeFile(path.join(dir, fileName), image);
  }
};

const signIn = (page: Page, theme: "dark" | "light") =>
  page.addInitScript((t) => {
    localStorage.setItem(
      "pl.auth.token",
      JSON.stringify({
        accessToken: "token",
        refreshToken: "refresh",
        expiresAt: Date.now() + 3600000,
      }),
    );
    localStorage.setItem("pl.theme", t);
  }, theme);

for (const theme of ["dark", "light"] as const) {
  for (const view of views) {
    test(`screenshot ${view.name} (${theme})`, async ({ page }, testInfo) => {
      await page.setViewportSize({ width: 1440, height: 900 });
      await signIn(page, theme);
      await stubApi(page);

      await page.goto(view.url);
      await view.ready(page);
      await page.evaluate(() => document.fonts.ready);

      await capture(page, testInfo, `${view.name}-${theme}.png`);
    });
  }

  test(`screenshot wishlist-empty (${theme})`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width: 1440, height: 900 });
    await signIn(page, theme);
    await stubApi(page);
    await page.route("**/api/wishlist**", (route) =>
      fulfillJson(route, pageOf([])),
    );

    await page.goto("/wishlist");
    await expect(page.getByRole("link", { name: "Ver lojas" })).toBeVisible();
    await page.evaluate(() => document.fonts.ready);

    await capture(page, testInfo, `wishlist-empty-${theme}.png`);
  });

  test(`screenshot mobile-menu (${theme})`, async ({ page }, testInfo) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await signIn(page, theme);
    await stubApi(page);

    await page.goto("/stores");
    await expect(page.getByText("Loud Coringa")).toBeVisible();
    await page.getByRole("button", { name: "Abrir menu" }).click();
    await expect(page.getByRole("dialog", { name: "Menu" })).toBeVisible();
    await page.evaluate(() => document.fonts.ready);

    await capture(page, testInfo, `mobile-menu-${theme}.png`);
  });
}
