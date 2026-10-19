import { test, expect, type Page } from "@playwright/test";
import { pageOf } from "./apiEnvelopes";

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

type Requester = {
  rawLabel: string;
  source: "Exact" | "Alias" | "Fuzzy";
  confidence: number;
  itemCount: number;
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
  requestedBy: Requester[];
};
type Alias = { rawLabel: string; labelCanonical: string };

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

const resolvedInput = (
  over: Partial<ResolvedInput> & { labelCanonical: string },
): ResolvedInput => ({
  updatedAt: "2026-04-01T00:00:00Z",
  requestedBy: [],
  ...over,
});

const requester = (
  over: Partial<Requester> & { rawLabel: string },
): Requester => ({
  source: "Exact",
  confidence: 100,
  itemCount: 1,
  ...over,
});

type ProfileState = {
  missingLabels: string[];
  unresolved: UnresolvedInput[];
  resolved: ResolvedInput[];
  canonicalLabels: string[];
  items?: (typeof baseStoreItem)[];
};

/**
 * The profile tab reads exactly one endpoint now, so the whole tab is wired from a single
 * mutable state object. `state` is read fresh on every request so a test can assert the
 * post-save (or post-delete) reload.
 */
const routeProfile = async (
  page: Page,
  state: ProfileState,
  hooks: {
    onPutValues?: (body: {
      values: { labelCanonical: string; value: string }[];
    }) => void;
    onPostAlias?: (body: Alias) => void;
    onDeleteValue?: (label: string | null) => void;
    onDeleteAlias?: (rawLabel: string | null) => void;
  } = {},
) => {
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
        items: state.items ?? [],
        missingLabels: state.missingLabels,
      }),
    });
  });

  await page.route("**/api/stores/store-1/profile", async (route) => {
    if (route.request().method() !== "GET") return route.fallback();
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

  // Regex, not glob: the delete label travels in the query string.
  await page.route(
    /\/api\/stores\/store-1\/profile\/values(\?|$)/,
    async (route) => {
      const method = route.request().method();
      if (method === "PUT") {
        hooks.onPutValues?.(JSON.parse(route.request().postData() ?? "{}"));
        await route.fulfill({ status: 204 });
        return;
      }
      if (method === "DELETE") {
        const label = new URL(route.request().url()).searchParams.get("label");
        hooks.onDeleteValue?.(label);
        await route.fulfill({ status: 204 });
        return;
      }
      return route.fallback();
    },
  );

  await page.route(
    /\/api\/stores\/store-1\/profile\/aliases(\?|$)/,
    async (route) => {
      const method = route.request().method();
      if (method === "POST") {
        hooks.onPostAlias?.(JSON.parse(route.request().postData() ?? "{}"));
        await route.fulfill({ status: 204 });
        return;
      }
      if (method === "DELETE") {
        const rawLabel = new URL(route.request().url()).searchParams.get(
          "rawLabel",
        );
        hooks.onDeleteAlias?.(rawLabel);
        await route.fulfill({ status: 204 });
        return;
      }
      return route.fallback();
    },
  );
};

const emptyState = (over: Partial<ProfileState> = {}): ProfileState => ({
  missingLabels: [],
  unresolved: [],
  resolved: [],
  canonicalLabels: [],
  ...over,
});

test("single profile panel renders, no separate Unresolved labels section", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({ unresolved: [unresolvedInput({ rawLabel: "steamid64" })] }),
  );

  await page.goto("/stores/store-1#profile");
  await expect(page.getByTestId("store-profile-editor")).toBeVisible();
  await expect(page.getByTestId("store-alias-confirmation")).toHaveCount(0);
  await expect(page.getByText("Unresolved labels")).toHaveCount(0);
});

test("every input shares one card shell and carries a single status pill", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      unresolved: [unresolvedInput({ rawLabel: "Nome no jogo" })],
      resolved: [
        resolvedInput({
          labelCanonical: "Twitch Username",
          requestedBy: [requester({ rawLabel: "Twitch Username" })],
        }),
        resolvedInput({ labelCanonical: "Steam ID" }),
      ],
      canonicalLabels: ["Twitch Username", "Steam ID"],
    }),
  );

  await page.goto("/stores/store-1#profile");

  for (const label of ["Nome no jogo", "Twitch Username", "Steam ID"]) {
    await expect(page.getByTestId(`profile-row-${label}`)).toHaveClass(/card/);
  }
  await expect(page.getByTestId("profile-status-Nome no jogo")).toContainText(
    /needs value/i,
  );
  await expect(
    page.getByTestId("profile-status-Twitch Username"),
  ).toContainText(/resolved/i);
  await expect(page.getByTestId("profile-status-Steam ID")).toContainText(
    /no item requests this/i,
  );
});

test("unresolved row offers set-value and map-to-existing, with or without metadata", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      unresolved: [
        unresolvedInput({
          rawLabel: "rich row",
          bestCanonical: "Steam ID",
          bestScore: 72,
          itemCount: 3,
          blockedEntryCount: 2,
        }),
        unresolvedInput({ rawLabel: "bare row" }),
      ],
      resolved: [resolvedInput({ labelCanonical: "Steam ID" })],
      canonicalLabels: ["Steam ID"],
    }),
  );

  await page.goto("/stores/store-1#profile");

  for (const label of ["rich row", "bare row"]) {
    await expect(page.getByTestId(`profile-mode-value-${label}`)).toBeEnabled();
    await expect(page.getByTestId(`profile-mode-alias-${label}`)).toBeEnabled();
  }
  await expect(page.getByTestId("profile-meta-rich row")).toContainText(
    "requested by 3 items",
  );
  await expect(page.getByTestId("profile-meta-rich row")).toContainText(
    "blocks 2 auto-redeem entries",
  );
  await expect(page.getByTestId("profile-bestmatch-rich row")).toContainText(
    "best match: Steam ID (72%)",
  );
});

test("Map to existing is disabled when there are no canonical labels", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({ unresolved: [unresolvedInput({ rawLabel: "steamid64" })] }),
  );

  await page.goto("/stores/store-1#profile");
  await expect(page.getByTestId("profile-mode-alias-steamid64")).toBeDisabled();
  await expect(page.getByTestId("profile-value-steamid64")).toBeVisible();
});

test("map an unresolved label to an existing canonical and save", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  let aliasBody: Alias | null = null;
  const state = emptyState({
    unresolved: [unresolvedInput({ rawLabel: "twitchh usrname" })],
    resolved: [resolvedInput({ labelCanonical: "Twitch Username" })],
    canonicalLabels: ["Twitch Username"],
  });
  await routeProfile(page, state, {
    onPostAlias: (body) => {
      aliasBody = body;
      state.unresolved = [];
      state.resolved = [
        resolvedInput({
          labelCanonical: "Twitch Username",
          requestedBy: [
            requester({ rawLabel: body.rawLabel, source: "Alias" }),
          ],
        }),
      ];
    },
  });

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-mode-alias-twitchh usrname").click();
  await page
    .getByTestId("profile-canonical-twitchh usrname")
    .selectOption("Twitch Username");
  await page.getByRole("button", { name: /Save profile/i }).click();

  await expect
    .poll(() => aliasBody)
    .toEqual({
      rawLabel: "twitchh usrname",
      labelCanonical: "Twitch Username",
    });
  // On reload the raw label stops being its own row and becomes a `fed by` chip.
  await expect(page.getByTestId("profile-row-twitchh usrname")).toHaveCount(0);
  await expect(
    page.getByTestId("profile-requester-twitchh usrname"),
  ).toBeVisible();
});

test("set a value on an unresolved label saves it under the raw label", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  let putBody: { values: { labelCanonical: string; value: string }[] } | null =
    null;
  const state = emptyState({
    unresolved: [unresolvedInput({ rawLabel: "E-mail" })],
  });
  await routeProfile(page, state, {
    onPutValues: (body) => {
      putBody = body;
      state.unresolved = [];
      state.resolved = [
        resolvedInput({
          labelCanonical: "E-mail",
          updatedAt: "2026-05-07T00:00:00Z",
          requestedBy: [requester({ rawLabel: "E-mail" })],
        }),
      ];
      state.canonicalLabels = ["E-mail"];
    },
  });

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-value-E-mail").fill("me@example.com");
  await page.getByRole("button", { name: /Save profile/i }).click();

  await expect
    .poll(() => putBody)
    .toEqual({
      values: [{ labelCanonical: "E-mail", value: "me@example.com" }],
    });
  await expect(page.getByTestId("profile-value-E-mail")).toHaveCount(0);
  await expect(page.getByTestId("profile-status-E-mail")).toContainText(
    /resolved/i,
  );
});

test("resolved row tags each requester chip with its source", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      resolved: [
        resolvedInput({
          labelCanonical: "Twitch Username",
          requestedBy: [
            requester({ rawLabel: "Twitch Username", source: "Exact" }),
            requester({ rawLabel: "twitchh usrname", source: "Alias" }),
            requester({
              rawLabel: "Username Twitch",
              source: "Fuzzy",
              confidence: 91,
            }),
          ],
        }),
      ],
      canonicalLabels: ["Twitch Username"],
    }),
  );

  await page.goto("/stores/store-1#profile");
  await expect(
    page.getByTestId("profile-requester-Twitch Username"),
  ).toContainText(/exact/i);
  await expect(
    page.getByTestId("profile-requester-twitchh usrname"),
  ).toContainText(/alias/i);
  await expect(
    page.getByTestId("profile-requester-Username Twitch"),
  ).toContainText("fuzzy 91%");
});

test("alias chip exposes a detach action; exact and fuzzy chips do not", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      resolved: [
        resolvedInput({
          labelCanonical: "Twitch Username",
          requestedBy: [
            requester({ rawLabel: "Twitch Username", source: "Exact" }),
            requester({ rawLabel: "twitchh usrname", source: "Alias" }),
            requester({
              rawLabel: "Username Twitch",
              source: "Fuzzy",
              confidence: 91,
            }),
          ],
        }),
      ],
      canonicalLabels: ["Twitch Username"],
    }),
  );

  await page.goto("/stores/store-1#profile");
  await expect(
    page.getByTestId("profile-detach-twitchh usrname"),
  ).toBeVisible();
  await expect(page.getByTestId("profile-detach-Twitch Username")).toHaveCount(
    0,
  );
  await expect(page.getByTestId("profile-detach-Username Twitch")).toHaveCount(
    0,
  );
});

test("detaching an alias sends DELETE with the raw label in the query string", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  let detached: string | null = null;
  const state = emptyState({
    resolved: [
      resolvedInput({
        labelCanonical: "Twitch Username",
        requestedBy: [requester({ rawLabel: "Nick (IGN)", source: "Alias" })],
      }),
    ],
    canonicalLabels: ["Twitch Username"],
  });
  await routeProfile(page, state, {
    onDeleteAlias: (rawLabel) => {
      detached = rawLabel;
      state.resolved = [resolvedInput({ labelCanonical: "Twitch Username" })];
    },
  });

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-detach-Nick (IGN)").click();

  await expect.poll(() => detached).toBe("Nick (IGN)");
  await expect(page.getByTestId("profile-requester-Nick (IGN)")).toHaveCount(0);
  await expect(
    page.getByTestId("profile-status-Twitch Username"),
  ).toContainText(/no item requests this/i);
});

test("delete confirmation names the requester count and hedges the outcome", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      resolved: [
        resolvedInput({
          labelCanonical: "Twitch Username",
          requestedBy: [
            requester({ rawLabel: "a" }),
            requester({ rawLabel: "b", source: "Alias" }),
            requester({ rawLabel: "c", source: "Fuzzy", confidence: 90 }),
          ],
        }),
      ],
      canonicalLabels: ["Twitch Username"],
    }),
  );

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-delete-Twitch Username").click();

  const warning = page.getByTestId("profile-delete-warning-Twitch Username");
  await expect(warning).toContainText("3 labels resolve here");
  await expect(warning).toContainText(/may leave them unresolved/i);
});

test("unrequested slot deletes with no impact warning", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      resolved: [resolvedInput({ labelCanonical: "Steam ID" })],
      canonicalLabels: ["Steam ID"],
    }),
  );

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-delete-Steam ID").click();

  await expect(
    page.getByTestId("profile-delete-confirm-Steam ID"),
  ).toBeVisible();
  await expect(page.getByTestId("profile-delete-warning-Steam ID")).toHaveCount(
    0,
  );
});

test("confirming delete sends the label encoded in the query string", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  let deletedLabel: string | null = null;
  const state = emptyState({
    resolved: [resolvedInput({ labelCanonical: "Discord tag / handle" })],
    canonicalLabels: ["Discord tag / handle"],
  });
  await routeProfile(page, state, {
    onDeleteValue: (label) => {
      deletedLabel = label;
      state.resolved = [];
      state.canonicalLabels = [];
    },
  });

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-delete-Discord tag / handle").click();
  await page.getByTestId("profile-delete-confirm-Discord tag / handle").click();

  await expect.poll(() => deletedLabel).toBe("Discord tag / handle");
  await expect(
    page.getByTestId("profile-row-Discord tag / handle"),
  ).toHaveCount(0);
});

test("single Save fans out: unresolved value + unresolved alias", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  let putBody: { values: { labelCanonical: string; value: string }[] } | null =
    null;
  let aliasBody: Alias | null = null;
  const state = emptyState({
    unresolved: [
      unresolvedInput({ rawLabel: "steamid64" }),
      unresolvedInput({
        rawLabel: "twitchx",
        bestCanonical: "Twitch Username",
        bestScore: 80,
      }),
    ],
    resolved: [resolvedInput({ labelCanonical: "Twitch Username" })],
    canonicalLabels: ["Twitch Username"],
  });
  await routeProfile(page, state, {
    onPutValues: (b) => (putBody = b),
    onPostAlias: (b) => (aliasBody = b),
  });

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-value-steamid64").fill("76561A");
  // Switching to alias mode prefills the picker with the server's best match.
  await page.getByTestId("profile-mode-alias-twitchx").click();
  await page.getByRole("button", { name: /Save profile/i }).click();

  await expect
    .poll(() => putBody)
    .toEqual({ values: [{ labelCanonical: "steamid64", value: "76561A" }] });
  await expect
    .poll(() => aliasBody)
    .toEqual({ rawLabel: "twitchx", labelCanonical: "Twitch Username" });
});

test("resolved row shows the updated time without exposing the value", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      resolved: [
        resolvedInput({
          labelCanonical: "Twitch Username",
          requestedBy: [requester({ rawLabel: "Twitch Username" })],
        }),
      ],
      canonicalLabels: ["Twitch Username"],
    }),
  );

  await page.goto("/stores/store-1#profile");
  const row = page.getByTestId("profile-row-Twitch Username");
  await expect(row.getByText(/updated/i)).toBeVisible();
  // The input is for replacement only; it must start empty (no plaintext round-trip).
  await expect(page.getByTestId("profile-input-Twitch Username")).toHaveValue(
    "",
  );
});

test("rows render in the order the server sent them", async ({ page }) => {
  await setupAuthenticatedUser(page);
  await routeProfile(
    page,
    emptyState({
      unresolved: [
        unresolvedInput({ rawLabel: "zzz blocked", blockedEntryCount: 4 }),
        unresolvedInput({ rawLabel: "aaa quiet" }),
      ],
      resolved: [resolvedInput({ labelCanonical: "mmm resolved" })],
      canonicalLabels: ["mmm resolved"],
    }),
  );

  await page.goto("/stores/store-1#profile");
  const rows = page.locator('[data-testid^="profile-row-"]');
  await expect(rows).toHaveCount(3);
  const labels = await rows.evaluateAll((found) =>
    found.map((r) => r.getAttribute("data-testid")),
  );
  expect(labels).toEqual([
    "profile-row-zzz blocked",
    "profile-row-aaa quiet",
    "profile-row-mmm resolved",
  ]);
});

test("sync adds new definition → blocked wishlist entry unblocks after profile fill", async ({
  page,
}) => {
  await setupAuthenticatedUser(page);

  let blocked = true;
  const state = emptyState({
    missingLabels: ["Discord Tag"],
    unresolved: [
      unresolvedInput({
        rawLabel: "Discord Tag",
        itemCount: 1,
        blockedEntryCount: 1,
      }),
    ],
    resolved: [
      resolvedInput({
        labelCanonical: "Email",
        requestedBy: [requester({ rawLabel: "Email" })],
      }),
    ],
    canonicalLabels: ["Email"],
    items: [
      {
        ...baseStoreItem,
        userInputDefinitions: ["Email", "Discord Tag"],
        wishlistEntryId: "wish-1",
      },
    ],
  });

  await page.route("**/api/wishlist", async (route) => {
    if (route.request().method() !== "GET") return route.fallback();
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(
        pageOf([
          {
            storeId: "store-1",
            storeName: "Test Store",
            isActive: true,
            lastSyncedAt: "2026-05-01T00:00:00Z",
            entries: [
              {
                id: "wish-1",
                storeItemId: "item-1",
                itemName: "VIP Badge",
                itemCost: 500,
                isAvailable: true,
                notifyOnAvailable: true,
                autoRedeem: !blocked,
                desiredRedemptions: 1,
                quotaUsed: 0,
                isBlocked: blocked,
                missingLabels: blocked ? ["Discord Tag"] : [],
                createdAt: "2026-04-01T00:00:00Z",
              },
            ],
          },
        ]),
      ),
    });
  });

  await routeProfile(page, state, {
    onPutValues: () => {
      state.missingLabels = [];
      state.unresolved = [];
      state.resolved = [
        ...state.resolved,
        resolvedInput({
          labelCanonical: "Discord Tag",
          updatedAt: "2026-05-07T00:00:00Z",
          requestedBy: [requester({ rawLabel: "Discord Tag" })],
        }),
      ];
      state.canonicalLabels = ["Email", "Discord Tag"];
      blocked = false;
    },
  });

  await page.goto("/wishlist");
  await expect(page.getByTestId("wishlist-blocked-wish-1")).toBeVisible();

  await page.goto("/stores/store-1#profile");
  await page.getByTestId("profile-value-Discord Tag").fill("xqc#1234");
  await page.getByRole("button", { name: /Save profile/i }).click();
  await expect(page.getByTestId("profile-status-Discord Tag")).toContainText(
    /resolved/i,
  );

  await page.goto("/wishlist");
  await expect(page.getByTestId("wishlist-blocked-wish-1")).toHaveCount(0);
});

// The add-store → missing-label modal flow (PUT /profile/values) is covered by
// MissingLabelDialog.test.tsx.

test("store deletion removes store from list", async ({ page }) => {
  await setupAuthenticatedUser(page);

  let stores = [
    {
      id: "store-1",
      channelId: "ch1",
      displayName: "Doomed Store",
      isActive: true,
      lastSyncedAt: null as string | null,
      itemCount: 0,
    },
  ];
  let deleteHit = false;

  await page.route("**/api/stores", async (route) => {
    if (route.request().method() !== "GET") return route.fallback();
    await route.fulfill({
      status: 200,
      contentType: "application/json",
      body: JSON.stringify(pageOf(stores)),
    });
  });

  await page.route("**/api/stores/store-1", async (route) => {
    if (route.request().method() === "DELETE") {
      deleteHit = true;
      stores = [];
      await route.fulfill({ status: 204 });
      return;
    }
    return route.fallback();
  });

  page.on("dialog", (dialog) => dialog.accept());

  await page.goto("/stores");
  await expect(page.getByText("Doomed Store")).toBeVisible();
  await page
    .getByRole("button", { name: /Remove/i })
    .first()
    .click();
  await expect.poll(() => deleteHit).toBe(true);
});
