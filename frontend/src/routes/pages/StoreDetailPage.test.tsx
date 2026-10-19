import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import { renderWithToasts } from "../../test/renderWithToasts";
import type { StoreItemDetail } from "../../features/stores/storesApi";
import StoreDetailPage from "./StoreDetailPage";

vi.mock("../../features/stores/useStores", () => ({
  useStoreDetail: vi.fn(),
}));

vi.mock("../../features/stores/useStoreItems", () => ({
  useStoreItems: vi.fn(),
}));

vi.mock("../../features/stores/useStoreItemFacets", () => ({
  useStoreItemFacets: () => ({
    categories: [],
    costBounds: { min: 0, max: 0 },
  }),
}));

vi.mock("../../features/stores/storesApi", () => ({
  fetchStoreProfile: vi.fn(),
  upsertStoreProfileValues: vi.fn().mockResolvedValue(undefined),
  upsertStoreProfileAlias: vi.fn().mockResolvedValue(undefined),
  deleteStoreProfileValue: vi.fn().mockResolvedValue(undefined),
  deleteStoreProfileAlias: vi.fn().mockResolvedValue(undefined),
}));

import { useStoreDetail } from "../../features/stores/useStores";
import { useStoreItems } from "../../features/stores/useStoreItems";
import { fetchStoreProfile } from "../../features/stores/storesApi";

const mockUseStoreDetail = vi.mocked(useStoreDetail);
const mockUseStoreItems = vi.mocked(useStoreItems);
const mockFetchProfile = vi.mocked(fetchStoreProfile);

const emptyProfile = { unresolved: [], resolved: [], canonicalLabels: [] };

const unresolvedItem = {
  rawLabel: "discord tagg",
  bestCanonical: "Email",
  bestScore: 70,
  aliasTarget: null,
  itemCount: 1,
  blockedEntryCount: 0,
};

const LocationProbe = () => {
  const location = useLocation();
  return <div data-testid="location-hash">{location.hash}</div>;
};

const renderWithRoute = (suffix = "") =>
  renderWithToasts(
    <MemoryRouter initialEntries={[`/stores/test-id${suffix}`]}>
      <Routes>
        <Route
          path="/stores/:id"
          element={
            <>
              <StoreDetailPage />
              <LocationProbe />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  );

// Annotated rather than inferred: every row here has `wishlistEntryId: null`, so inference
// narrows the property to `null` and a fixture that overrides it stops type-checking.
const items: StoreItemDetail[] = [
  {
    id: "item1",
    providerItemId: "se1",
    name: "Cool Item",
    description: "A cool item",
    cost: 500,
    categoryName: "rewards",
    subscriberOnly: false,
    isAvailable: true,
    quantityCurrent: 5,
    quantityTotal: 10,
    allowMessages: false,
    thumbnailUrl: null,
    wishlistEntryId: null,
    providerCreatedAt: "2026-01-01T00:00:00Z",
  },
  {
    id: "item2",
    providerItemId: "se2",
    name: "Hidden Item",
    description: null,
    cost: 200,
    categoryName: "sounds",
    subscriberOnly: false,
    isAvailable: false,
    quantityCurrent: 0,
    quantityTotal: 5,
    allowMessages: false,
    thumbnailUrl: null,
    wishlistEntryId: null,
    providerCreatedAt: "2026-01-02T00:00:00Z",
  },
];

/** The envelope the page now receives: no items, but the store's unfiltered count. */
const storeWithItems = {
  id: "test-id",
  channelId: "ch1",
  displayName: "My Store",
  isActive: true,
  lastSyncedAt: null,
  createdAt: "2026-03-01T00:00:00Z",
  missingLabels: [],
  itemCount: items.length,
  provider: "streamelements" as const,
};

const itemsResult = (rows: typeof items) => ({
  items: rows,
  totalCount: rows.length,
  status: "ready" as const,
  isRefreshing: false,
  hasMore: false,
  loadMore: vi.fn(),
  reload: vi.fn(),
});

beforeEach(() => {
  vi.clearAllMocks();
  mockFetchProfile.mockResolvedValue(emptyProfile);
  mockUseStoreItems.mockReturnValue(itemsResult(items));
});

const readyStore = () => {
  mockUseStoreDetail.mockReturnValue({
    store: storeWithItems,
    status: "ready",
  });
};

describe("StoreDetailPage", () => {
  it("shows loading state", () => {
    mockUseStoreDetail.mockReturnValue({ store: null, status: "loading" });
    renderWithRoute();
    expect(screen.getByText("Loading store...")).toBeInTheDocument();
  });

  it("shows not found state", () => {
    mockUseStoreDetail.mockReturnValue({ store: null, status: "not_found" });
    renderWithRoute();
    expect(screen.getByText("Store not found.")).toBeInTheDocument();
  });

  it("renders store detail with items", () => {
    mockUseStoreDetail.mockReturnValue({
      store: { ...storeWithItems, itemCount: 1 },
      status: "ready",
    });
    mockUseStoreItems.mockReturnValue(itemsResult([items[0]]));
    renderWithRoute();
    expect(screen.getByText("My Store")).toBeInTheDocument();
    expect(screen.getByText("Cool Item")).toBeInTheDocument();
    expect(screen.getByText("500")).toBeInTheDocument();
    expect(screen.getAllByText("Available").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Stock: 5 / 10")).toBeInTheDocument();
  });

  it("Search button toggles filter panel", async () => {
    readyStore();
    renderWithRoute();
    expect(
      screen.queryByRole("searchbox", { name: /search items/i }),
    ).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(
      screen.getByRole("searchbox", { name: /search items/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Available" }),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(
      screen.queryByRole("searchbox", { name: /search items/i }),
    ).not.toBeInTheDocument();
  });

  it("header shows total item count regardless of filters", () => {
    readyStore();
    // avail=0 shows all; default available=true would filter to 1 item
    renderWithRoute("?avail=0");
    // 2 total items always shown in header
    expect(screen.getByText(/2 items/)).toBeInTheDocument();
  });

  /**
   * Narrowing moved to the server, so what this page owes is the *request*, not the subset. It
   * renders whatever comes back; asserting it hides a row would be asserting a rule it no longer
   * holds.
   */
  it("asks for available items only by default", () => {
    readyStore();
    renderWithRoute();

    expect(mockUseStoreItems).toHaveBeenCalledWith(
      "test-id",
      expect.objectContaining({ available: true }),
    );
  });

  it("a store with no items says so and offers nothing to clear", () => {
    mockUseStoreDetail.mockReturnValue({
      store: { ...storeWithItems, itemCount: 0 },
      status: "ready",
    });
    mockUseStoreItems.mockReturnValue(itemsResult([]));
    renderWithRoute();

    expect(
      screen.getByRole("heading", { name: "This store has no items" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Clear filters" }),
    ).not.toBeInTheDocument();
  });

  it("filters matching nothing offer to clear them", async () => {
    readyStore();
    mockUseStoreItems.mockReturnValue(itemsResult([]));
    renderWithRoute("?avail=0");

    expect(
      screen.getByRole("heading", { name: "No items match" }),
    ).toBeInTheDocument();
    await userEvent.click(
      screen.getByRole("button", { name: "Clear filters" }),
    );

    expect(mockUseStoreItems).toHaveBeenLastCalledWith(
      "test-id",
      expect.objectContaining({ available: true }),
    );
  });

  it("shows placeholder cards while the first page of items loads", () => {
    readyStore();
    mockUseStoreItems.mockReturnValue({
      ...itemsResult([]),
      status: "loading",
    });
    renderWithRoute();

    expect(screen.getByRole("status")).toHaveTextContent("Loading store...");
    expect(
      screen.queryByRole("heading", { name: "No items match" }),
    ).not.toBeInTheDocument();
  });

  it("passes avail=0 from the URL through to the request", () => {
    readyStore();
    renderWithRoute("?avail=0");

    expect(mockUseStoreItems).toHaveBeenCalledWith(
      "test-id",
      expect.objectContaining({ available: false }),
    );
  });

  it("renders the rows the server returned, in the order given", () => {
    readyStore();
    renderWithRoute("?avail=0");

    expect(screen.getByText("Cool Item")).toBeInTheDocument();
    expect(screen.getByText("Hidden Item")).toBeInTheDocument();
  });

  it("back button navigates to /stores", async () => {
    readyStore();
    renderWithToasts(
      <MemoryRouter initialEntries={["/stores/test-id"]}>
        <Routes>
          <Route path="/stores/:id" element={<StoreDetailPage />} />
          <Route
            path="/stores"
            element={<div data-testid="stores-page">Stores</div>}
          />
        </Routes>
      </MemoryRouter>,
    );
    await userEvent.click(
      screen.getByRole("button", { name: /back to stores/i }),
    );
    expect(screen.getByTestId("stores-page")).toBeInTheDocument();
  });

  it("sends a picked category to the server rather than filtering locally", async () => {
    readyStore();
    renderWithRoute("?avail=0&cat=rewards");

    expect(mockUseStoreItems).toHaveBeenCalledWith(
      "test-id",
      expect.objectContaining({ categories: ["rewards"] }),
    );
  });
});

describe("StoreDetailPage tabs", () => {
  it("defaults to Items tab with grid visible and Profile content not rendered", async () => {
    readyStore();
    renderWithRoute();
    expect(screen.getByRole("tab", { name: /items/i })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.getByText("Cool Item")).toBeInTheDocument();
    await waitFor(() => expect(mockFetchProfile).toHaveBeenCalledTimes(1));
    expect(
      screen.queryByTestId("store-profile-editor"),
    ).not.toBeInTheDocument();
  });

  it("#profile hash selects Profile tab with the merged editor (single panel)", async () => {
    mockFetchProfile.mockResolvedValue({
      ...emptyProfile,
      unresolved: [unresolvedItem],
      canonicalLabels: ["Email"],
    });
    readyStore();
    renderWithRoute("#profile");
    expect(screen.getByRole("tab", { name: /profile/i })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(await screen.findByTestId("store-profile-editor")).toBeVisible();
    // No separate "Unresolved labels" panel; the unresolved row lives in the editor.
    expect(
      screen.queryByTestId("store-alias-confirmation"),
    ).not.toBeInTheDocument();
    expect(await screen.findByTestId("profile-row-discord tagg")).toBeVisible();
    expect(screen.queryByText("Cool Item")).not.toBeInTheDocument();
  });

  it("unknown hash falls back to Items tab", () => {
    readyStore();
    renderWithRoute("#something-else");
    expect(screen.getByRole("tab", { name: /items/i })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(screen.getByText("Cool Item")).toBeInTheDocument();
  });

  it("tab clicks update the location hash", async () => {
    readyStore();
    renderWithRoute();
    await userEvent.click(screen.getByRole("tab", { name: /profile/i }));
    expect(screen.getByTestId("location-hash")).toHaveTextContent("#profile");
    expect(await screen.findByTestId("store-profile-editor")).toBeVisible();

    await userEvent.click(screen.getByRole("tab", { name: /items/i }));
    expect(screen.getByTestId("location-hash")).toBeEmptyDOMElement();
    expect(screen.getByText("Cool Item")).toBeInTheDocument();
  });

  it("fetches the profile exactly once on load", async () => {
    readyStore();
    renderWithRoute();
    await waitFor(() => expect(mockFetchProfile).toHaveBeenCalledTimes(1));
  });

  it("still flags the tab when the profile fetch fails", async () => {
    // One endpoint now, so a failure blanks the editor — but the store's own missingLabels
    // still light the dot, so the user is not told everything is fine.
    mockFetchProfile.mockRejectedValue(new Error("500"));
    mockUseStoreDetail.mockReturnValue({
      store: { ...storeWithItems, missingLabels: ["Discord Tag"] },
      status: "ready",
    });
    renderWithRoute("#profile");

    expect(
      await screen.findByText("Failed to load store profile."),
    ).toBeVisible();
    const profileTab = screen.getByRole("tab", { name: /profile/i });
    expect(
      await within(profileTab).findByTestId("notification-dot"),
    ).toBeInTheDocument();
  });
});

describe("StoreDetailPage notification dot", () => {
  it("shows dot when the profile has unresolved inputs", async () => {
    mockFetchProfile.mockResolvedValue({
      ...emptyProfile,
      unresolved: [{ ...unresolvedItem, rawLabel: "Discord Tag" }],
    });
    readyStore();
    renderWithRoute();
    const profileTab = screen.getByRole("tab", { name: /profile/i });
    expect(
      await within(profileTab).findByTestId("notification-dot"),
    ).toBeInTheDocument();
  });

  it("shows dot when store detail has missing labels", async () => {
    mockUseStoreDetail.mockReturnValue({
      store: { ...storeWithItems, missingLabels: ["Steam ID"] },
      status: "ready",
    });
    renderWithRoute();
    const profileTab = screen.getByRole("tab", { name: /profile/i });
    expect(
      await within(profileTab).findByTestId("notification-dot"),
    ).toBeInTheDocument();
  });

  it("shows dot when the only problem is an unmapped raw label", async () => {
    mockFetchProfile.mockResolvedValue({
      ...emptyProfile,
      unresolved: [unresolvedItem],
    });
    readyStore();
    renderWithRoute();
    const profileTab = screen.getByRole("tab", { name: /profile/i });
    expect(
      await within(profileTab).findByTestId("notification-dot"),
    ).toBeInTheDocument();
  });

  it("shows no dot when configuration is complete", async () => {
    readyStore();
    renderWithRoute();
    await waitFor(() => expect(mockFetchProfile).toHaveBeenCalledTimes(1));
    await waitFor(() =>
      expect(screen.queryByTestId("notification-dot")).not.toBeInTheDocument(),
    );
  });

  it("shows no dot while profile data is loading", () => {
    mockFetchProfile.mockReturnValue(new Promise(() => {}));
    mockUseStoreDetail.mockReturnValue({
      store: { ...storeWithItems, missingLabels: ["Steam ID"] },
      status: "ready",
    });
    renderWithRoute();
    expect(screen.queryByTestId("notification-dot")).not.toBeInTheDocument();
  });

  it("dot carries screen-reader-only text", async () => {
    mockFetchProfile.mockResolvedValue({
      ...emptyProfile,
      unresolved: [{ ...unresolvedItem, rawLabel: "Discord Tag" }],
    });
    readyStore();
    renderWithRoute();
    const dot = await screen.findByTestId("notification-dot");
    const srText = dot.querySelector(".sr-only");
    expect(srText).not.toBeNull();
    expect(srText!.textContent!.length).toBeGreaterThan(0);
  });

  it("dot clears after setting a value for the last unresolved input", async () => {
    mockFetchProfile
      .mockResolvedValueOnce({
        ...emptyProfile,
        unresolved: [{ ...unresolvedItem, rawLabel: "Discord Tag" }],
      })
      .mockResolvedValue(emptyProfile);
    readyStore();
    renderWithRoute("#profile");

    await screen.findByTestId("notification-dot");
    await userEvent.type(
      screen.getByTestId("profile-value-Discord Tag"),
      "user#1234",
    );
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() =>
      expect(screen.queryByTestId("notification-dot")).not.toBeInTheDocument(),
    );
  });

  it("dot clears after mapping the last unresolved label to an existing value", async () => {
    mockFetchProfile
      .mockResolvedValueOnce({
        ...emptyProfile,
        unresolved: [unresolvedItem],
        canonicalLabels: ["Email"],
      })
      .mockResolvedValue(emptyProfile);
    readyStore();
    renderWithRoute("#profile");

    await screen.findByTestId("notification-dot");
    // Switching to alias mode prefills the picker with the server's best match ("Email"),
    // so a single Save resolves the label.
    await userEvent.click(
      screen.getByTestId("profile-mode-alias-discord tagg"),
    );
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() =>
      expect(screen.queryByTestId("notification-dot")).not.toBeInTheDocument(),
    );
  });
});

/**
 * Rows deliberately stay on screen while the server re-orders them, so without a pending signal a
 * filter change or a sort produces no visible change at all until the response lands — a slow query
 * is indistinguishable from a dead control. The hook already derives `isRefreshing`; this is the
 * page honouring it.
 */
describe("StoreDetailPage — pending feedback while the server re-queries", () => {
  it("marks the grid busy while a new query is in flight", async () => {
    readyStore();
    mockUseStoreItems.mockReturnValue({
      ...itemsResult(items),
      isRefreshing: true,
    });

    renderWithRoute();

    await waitFor(() =>
      expect(screen.getByTestId("store-item-grid")).toHaveAttribute(
        "aria-busy",
        "true",
      ),
    );
  });

  it("leaves the grid unmarked once the rows match the query", async () => {
    readyStore();
    mockUseStoreItems.mockReturnValue(itemsResult(items));

    renderWithRoute();

    await waitFor(() =>
      expect(screen.getByTestId("store-item-grid")).toHaveAttribute(
        "aria-busy",
        "false",
      ),
    );
  });
});

/**
 * An already-wishlisted card's button edits the entry, which means fetching its detail before the
 * modal can open. A failure in that fetch has to reach the user, or the button just looks dead; the
 * success path is pinned here too.
 */
describe("StoreDetailPage — editing a wishlisted item", () => {
  const wishlistedItems = [{ ...items[0], wishlistEntryId: "entry-1" }];

  /** A backend error apiClient will parse into an ApiError, i.e. one carrying `code`. */
  const problem = (status: number, body: Record<string, unknown>) =>
    HttpResponse.json(body, {
      status,
      headers: { "Content-Type": "application/problem+json" },
    });

  const toasts = () => document.querySelectorAll("li.toast");

  const clickWishlisted = async () => {
    readyStore();
    mockUseStoreItems.mockReturnValue(itemsResult(wishlistedItems));
    renderWithRoute();
    await userEvent.click(screen.getByRole("button", { name: /wishlisted/i }));
  };

  it("raises an error toast and keeps the modal shut when the fetch fails", async () => {
    server.use(
      http.get(
        "http://localhost/api/wishlist/:id",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );

    await clickWishlisted();

    const toast = await screen.findByText("Failed to load wishlist entry");
    expect(toast.closest("[data-tone]")).toHaveAttribute("data-tone", "error");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("shows the backend's reason when the fetch is refused with a code", async () => {
    server.use(
      http.get("http://localhost/api/wishlist/:id", () =>
        problem(404, { code: "wishlist.entry_not_found" }),
      ),
    );

    await clickWishlisted();

    expect(
      await screen.findByText("Wishlist entry not found."),
    ).toBeInTheDocument();
  });

  it("opens the edit modal and stays silent when the fetch succeeds", async () => {
    await clickWishlisted();

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(toasts()).toHaveLength(0);
  });

  // The same button adds when there is no entry yet, and that path never touches the network.
  it("opens the add modal with no fetch for an unwishlisted item", async () => {
    readyStore();
    mockUseStoreItems.mockReturnValue(itemsResult([items[0]]));
    renderWithRoute();

    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(toasts()).toHaveLength(0);
  });
});
