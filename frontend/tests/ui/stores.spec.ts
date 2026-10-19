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

test("add a store and verify items are displayed", async ({ page }) => {
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
          missingLabels: [],
          itemCount: 1,
        }),
      });
    }
  });

  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();

  // On open: Continue disabled, no Display Name field, no Sync interval
  await expect(page.getByRole("button", { name: /Continue/i })).toBeDisabled();
  await expect(page.getByText("Display name")).not.toBeVisible();
  await expect(page.getByText("Sync interval")).not.toBeVisible();

  await page.getByPlaceholder("e.g. xqc").fill("teststreamer");
  await page.getByRole("button", { name: "Lookup" }).click();

  // After lookup resolves: found card visible, Continue enabled, no Display Name editable field
  await expect(page.getByText("Test Store")).toBeVisible();
  await expect(page.getByRole("button", { name: /Continue/i })).toBeEnabled();
  await expect(page.getByText("Display name")).not.toBeVisible();

  // Register updated GET route before Continue (single click creates store)
  await page.route("**/api/stores", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              id: "store-1",
              channelId: "ch1",
              displayName: "Test Store",
              isActive: true,
              lastSyncedAt: "2026-03-01T00:00:00Z",
              itemCount: 1,
            },
          ]),
        ),
      });
    } else {
      await route.fallback();
    }
  });

  // Single Continue click submits creation
  await page.getByRole("button", { name: /Continue/i }).click();

  await expect(page.getByText("Test Store")).toBeVisible();
  // `stores.itemCount` is pluralized in the catalog, so a single item reads "1 item".
  await expect(page.getByText("1 item", { exact: true })).toBeVisible();

  // Navigate to store detail. The grid fetches its own slice, so the items are stubbed separately
  // from the store envelope.
  await stubStoreItems(page, "store-1", [
    {
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
      thumbnailUrl: null,
      wishlistEntryId: null,
      providerCreatedAt: "2026-01-01T00:00:00Z",
    },
  ]);

  await page.route("**/api/stores/store-1", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        id: "store-1",
        channelId: "ch1",
        displayName: "Test Store",
        isActive: true,
        lastSyncedAt: null,
        createdAt: "2026-03-01T00:00:00Z",
        missingLabels: [],
        itemCount: 1,
      }),
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

  await page.getByRole("button", { name: /view items/i }).click();

  await expect(page.getByText("Cool Item", { exact: true })).toBeVisible();
  await expect(page.getByText("500")).toBeVisible();
  // Availability pill
  await expect(page.locator(".pill.ok").first()).toBeVisible();
});

test("deactivate a store and verify status change", async ({ page }) => {
  await setupAuthenticatedUser(page);

  let storeActive = true;

  await page.route("**/api/stores", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(
          pageOf([
            {
              id: "store-1",
              channelId: "ch1",
              displayName: "My Store",
              isActive: storeActive,
              lastSyncedAt: null,
              itemCount: 3,
            },
          ]),
        ),
      });
    }
  });

  await page.route("**/api/stores/store-1/deactivate", async (route) => {
    storeActive = false;
    await route.fulfill({ status: 204 });
  });

  await page.goto("/stores");

  await expect(page.getByText("Active", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: /pause/i }).click();

  await expect(
    page.locator(".store-card").getByText("Paused", { exact: true }),
  ).toBeVisible();
});

test("remove a store and verify it disappears from the list", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  let stores = [
    {
      id: "store-1",
      channelId: "ch1",
      displayName: "Store to Remove",
      isActive: true,
      lastSyncedAt: null,
      itemCount: 2,
    },
  ];

  await page.route("**/api/stores", async (route) => {
    if (route.request().method() === "GET") {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(pageOf(stores)),
      });
    }
  });

  await page.route("**/api/stores/store-1", async (route) => {
    if (route.request().method() === "DELETE") {
      stores = [];
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: "{}",
      });
    } else {
      await route.fallback();
    }
  });

  await page.goto("/stores");

  await expect(page.getByText("Store to Remove")).toBeVisible();
  await page.getByRole("button", { name: "Remove" }).click();

  await expect(
    page.getByRole("heading", { name: "No stores yet" }),
  ).toBeVisible();
});

test("lookup failure shows error and keeps Add Store disabled", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/stores/lookup*", async (route) => {
    await route.fulfill({
      status: 404,
      contentType: "application/problem+json",
      body: JSON.stringify({
        title: "Channel not found",
        status: 404,
        code: "store_lookup.not_found",
        params: { provider: "streamelements", query: "nobody" },
      }),
    });
  });

  await page.route("**/api/stores", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(pageOf([])),
    });
  });

  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();

  await page.getByPlaceholder("e.g. xqc").fill("nobody");
  await page.getByRole("button", { name: "Lookup" }).click();

  await expect(page.getByText(/No store found for 'nobody'/)).toBeVisible();
  await expect(page.getByRole("button", { name: "Continue →" })).toBeDisabled();
});

test("pause shows 'Store paused' toast", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/stores", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        pageOf([
          {
            id: "store-1",
            channelId: "ch1",
            displayName: "My Store",
            isActive: true,
            lastSyncedAt: null,
            itemCount: 3,
          },
        ]),
      ),
    });
  });

  await page.route("**/api/stores/store-1/deactivate", async (route) => {
    await route.fulfill({ status: 204 });
  });

  await page.goto("/stores");
  await page.getByRole("button", { name: /pause/i }).click();

  await expect(page.getByText("Store paused", { exact: true })).toBeVisible();
});

test("resume shows 'Store resumed' toast and calls activate endpoint", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  let storeActive = false;

  await page.route("**/api/stores", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        pageOf([
          {
            id: "store-1",
            channelId: "ch1",
            displayName: "My Store",
            isActive: storeActive,
            lastSyncedAt: null,
            itemCount: 3,
          },
        ]),
      ),
    });
  });

  await page.route("**/api/stores/store-1/activate", async (route) => {
    storeActive = true;
    await route.fulfill({ status: 204 });
  });

  await page.goto("/stores");

  await expect(page.getByText("Paused", { exact: true })).toBeVisible();
  await page.getByRole("button", { name: /resume/i }).click();

  await expect(page.getByText("Store resumed", { exact: true })).toBeVisible();
  await expect(page.getByText("Active", { exact: true })).toBeVisible();
});

test("remove shows 'Store removed' toast", async ({ page }) => {
  await setupAuthenticatedUser(page);

  let stores = [
    {
      id: "store-1",
      channelId: "ch1",
      displayName: "My Store",
      isActive: true,
      lastSyncedAt: null,
      itemCount: 3,
    },
  ];

  await page.route("**/api/stores", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(pageOf(stores)),
    });
  });

  await page.route("**/api/stores/store-1", async (route) => {
    if (route.request().method() === "DELETE") {
      stores = [];
      await route.fulfill({ status: 204 });
    } else {
      await route.fallback();
    }
  });

  await page.goto("/stores");
  await page.getByRole("button", { name: "Remove" }).click();

  await expect(page.getByText("Store removed", { exact: true })).toBeVisible();
  await expect(
    page.getByRole("heading", { name: "No stores yet" }),
  ).toBeVisible();
});

test("action error shows error toast with data-tone=error", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/stores", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        pageOf([
          {
            id: "store-1",
            channelId: "ch1",
            displayName: "My Store",
            isActive: true,
            lastSyncedAt: null,
            itemCount: 3,
          },
        ]),
      ),
    });
  });

  await page.route("**/api/stores/store-1/deactivate", async (route) => {
    await route.fulfill({
      status: 500,
      contentType: "application/json",
      body: "{}",
    });
  });

  await page.goto("/stores");
  await page.getByRole("button", { name: /pause/i }).click();

  const toast = page.locator(".toast[data-tone='error']");
  await expect(toast).toBeVisible();
  await expect(toast.getByText("Failed to update store status")).toBeVisible();
});

test("verify add store is blocked when no store credential is configured", async ({
  page,
}) => {
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
        credentials: [],
      }),
    });
  });

  await page.route("**/api/stores", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(pageOf([])),
    });
  });

  await page.goto("/stores");

  await expect(
    page.getByText("Add a store credential in Settings to add stores."),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Add Store" }),
  ).not.toBeVisible();
});

test("focus moves to main landmark after route change", async ({ page }) => {
  await setupAuthenticatedUser(page);

  await page.route("**/api/wishlist", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(pageOf([])),
    });
  });

  await page.goto("/stores");
  await page.getByRole("link", { name: /Wishlist/ }).click();
  await page.waitForURL("**/wishlist");

  await page.waitForFunction(
    () => document.activeElement?.id === "main-content",
    { timeout: 3000 },
  );
});

test("Tab after opening AddStoreDialog keeps focus inside dialog", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();
  await expect(page.getByRole("dialog")).toBeVisible();

  // Tab multiple times and verify focus stays inside dialog
  for (let i = 0; i < 5; i++) {
    await page.keyboard.press("Tab");
    const isInsideDialog = await page.evaluate(() => {
      const dialog = document.querySelector('[role="dialog"]');
      return dialog?.contains(document.activeElement) ?? false;
    });
    expect(isInsideDialog).toBe(true);
  }
});

test("Escape closes AddStoreDialog and returns focus to Add Store button", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await page.goto("/stores");
  await page.getByRole("button", { name: "Add Store" }).first().click();
  await expect(page.getByRole("dialog")).toBeVisible();

  await page.keyboard.press("Escape");

  await expect(page.getByRole("dialog")).not.toBeVisible();
  const activeEl = await page.evaluate(
    () =>
      (document.activeElement as HTMLElement | null)?.textContent?.trim() ?? "",
  );
  expect(activeEl).toContain("Add Store");
});

/**
 * The active/paused pills come from `GET /api/stores/summary`, not from the rows the page holds, so
 * a mutation has to refresh them deliberately — `reload()` refetching only the list leaves the
 * headline describing the state before the toggle.
 *
 * Asserted through the rendered pills rather than the request count, because the visible staleness
 * is the defect: the same gap makes the "N / M watched" label read "1 / 0" after the first store is
 * added, since `M` is `activeCount + pausedCount`.
 */
test("headline counts refresh after a store is paused", async ({ page }) => {
  await setupAuthenticatedUser(page);

  let storeActive = true;

  await page.route("**/api/stores", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        pageOf([
          {
            id: "store-1",
            channelId: "ch1",
            displayName: "My Store",
            isActive: storeActive,
            lastSyncedAt: null,
            itemCount: 3,
          },
        ]),
      ),
    });
  });

  await page.route("**/api/stores/summary", async (route) => {
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify({
        activeCount: storeActive ? 1 : 0,
        pausedCount: storeActive ? 0 : 1,
      }),
    });
  });

  await page.route("**/api/stores/store-1/deactivate", async (route) => {
    storeActive = false;
    await route.fulfill({ status: 204 });
  });

  await page.goto("/stores");
  await expect(page.getByText("1 Active")).toBeVisible();

  await page.getByRole("button", { name: /pause/i }).click();

  await expect(page.getByText("1 Paused")).toBeVisible();
  await expect(page.getByText("1 Active")).toHaveCount(0);
});

// The distinct input labels a real StreamElements store (Gaules) asks for. A freshly added store
// has no stored values, so every one of them lands in the missing-label dialog at once.
const manyMissingLabels = [
  "Steam Trade Link",
  "E-mail",
  "Email",
  "Stream trade link",
  "Email completo",
  "Nome Completo",
  "Cpf",
  "Dados Da Conta / Pix",
  "Endereço Completo Com Cep",
  "Cidade E Estado",
  "email que deseja receber o vídeo",
  "nome da pessoa para o gau mandar o salve",
  "alguma obs especial para o salve",
  "a",
  "Perfil da Gc",
  "Perfil Gc",
  "Perifl Gc",
  "Steam trade linnk",
  "Tamanho Desejado",
  "Endereço Completo (Com numero)",
  "Cidade",
  "Estado",
  "Bairro",
  "CEP",
  "Telefone para contato (Caso necessário)",
  "Telefone para contato",
  "Tamanho (Somente entre M | G | GG)",
  "Nick Twitch",
  "Link Steam (Deixe o perfil desbloqueado)",
  "Nome Competo",
];

for (const viewport of [
  { width: 1280, height: 720 },
  { width: 390, height: 664 },
]) {
  test(`missing-label dialog keeps its title and actions on screen at ${viewport.width}x${viewport.height}`, async ({
    page,
  }) => {
    await page.setViewportSize(viewport);
    await setupAuthenticatedUser(page);

    await page.route("**/api/stores/lookup*", async (route) => {
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify({
          externalId: "ch1",
          handle: "gaules",
          displayName: "Gaules",
          avatarUrl: null,
          itemCount: null,
        }),
      });
    });

    await page.route("**/api/stores", async (route) => {
      if (route.request().method() === "POST") {
        await route.fulfill({
          status: 201,
          contentType: "application/json",
          body: JSON.stringify({
            id: "store-1",
            channelId: "ch1",
            displayName: "Gaules",
            isActive: true,
            lastSyncedAt: null,
            createdAt: "2026-03-01T00:00:00Z",
            missingLabels: manyMissingLabels,
            itemCount: 280,
          }),
        });
        return;
      }
      await route.fulfill({
        status: 200,
        contentType: "application/json",
        body: JSON.stringify(pageOf([])),
      });
    });

    await page.goto("/stores");
    await page.getByRole("button", { name: "Add Store" }).first().click();
    await page.getByPlaceholder("e.g. xqc").fill("gaules");
    await page.getByRole("button", { name: "Lookup" }).click();
    await page.getByRole("button", { name: /Continue/i }).click();

    const dialog = page.getByRole("dialog");
    const title = dialog.getByRole("heading", { name: /store profile/i });
    const skip = dialog.getByRole("button", { name: "Skip for now" });
    const save = dialog.getByRole("button", { name: "Save profile" });
    const lastField = dialog.getByLabel("Nome Competo", { exact: true });

    await expect(title).toBeInViewport();
    await expect(skip).toBeInViewport();
    await expect(save).toBeInViewport();

    await lastField.scrollIntoViewIfNeeded();
    await expect(lastField).toBeInViewport();
    await expect(title).toBeInViewport();
    await expect(save).toBeInViewport();
  });
}
