import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { renderWithToasts } from "../../test/renderWithToasts";

vi.mock("../../features/wishlist/useWishlist", () => ({
  useWishlistList: vi.fn(),
  useRemoveWishlist: () => ({
    execute: vi.fn().mockResolvedValue({ ok: true }),
    status: "idle",
  }),
  useWishlistEntryDetail: () => ({
    execute: vi.fn().mockResolvedValue({ ok: true, entry: null }),
    status: "idle",
  }),
}));

vi.mock("../../features/wishlist/wishlistApi", () => ({
  fetchWishlistGrouped: vi.fn(),
  fetchWishlistEntry: vi.fn(),
  removeWishlistEntry: vi.fn(),
}));

import WishlistPage from "./WishlistPage";
import { useWishlistList } from "../../features/wishlist/useWishlist";
const mockUseWishlistList = vi.mocked(useWishlistList);

const makeData = () => ({
  ...pageMeta,
  items: [
    {
      storeId: "s1",
      storeName: "Store Alpha",
      isActive: true,
      lastSyncedAt: null,
      entries: [
        {
          id: "e1",
          storeItemId: "item1",
          itemName: "Cool Skin",
          itemCost: 500,
          isAvailable: true,
          notifyOnAvailable: true,
          autoRedeem: false,
          desiredRedemptions: 1,
          quotaUsed: 0,
          isBlocked: false,
          missingLabels: [],
          createdAt: "2026-04-01T00:00:00Z",
        },
      ],
    },
    {
      storeId: "s2",
      storeName: "Store Beta",
      isActive: false,
      lastSyncedAt: null,
      entries: [
        {
          id: "e2",
          storeItemId: "item2",
          itemName: "Rare Item",
          itemCost: 9000,
          isAvailable: false,
          notifyOnAvailable: false,
          autoRedeem: false,
          desiredRedemptions: 1,
          quotaUsed: 0,
          isBlocked: false,
          missingLabels: [],
          createdAt: "2026-04-02T00:00:00Z",
        },
      ],
    },
  ],
});

/** Only `items` varies between these fixtures, so the rest of the envelope is spread in. */
const pageMeta = { totalCount: 0, offset: 0, limit: 0 };

describe("WishlistPage", () => {
  it("shows loading state", () => {
    mockUseWishlistList.mockReturnValue({
      data: null,
      status: "loading",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });
    expect(screen.getByText("Loading wishlist...")).toBeInTheDocument();
  });

  it("shows error state", () => {
    mockUseWishlistList.mockReturnValue({
      data: null,
      status: "error",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });
    expect(screen.getByText("Failed to load wishlist.")).toBeInTheDocument();
  });

  it("grouped by store — renders both store names", () => {
    mockUseWishlistList.mockReturnValue({
      data: makeData(),
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });
    expect(screen.getByText("Store Alpha")).toBeInTheDocument();
    expect(screen.getByText("Store Beta")).toBeInTheDocument();
  });

  it("renders entries under their respective stores", () => {
    mockUseWishlistList.mockReturnValue({
      data: makeData(),
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });
    expect(screen.getByText("Cool Skin")).toBeInTheDocument();
    expect(screen.getByText("Rare Item")).toBeInTheDocument();
  });

  it("Notify badge shown for entry with notifyOnAvailable=true", () => {
    mockUseWishlistList.mockReturnValue({
      data: makeData(),
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });
    expect(screen.getByText("Notify")).toBeInTheDocument();
  });

  it("empty wishlist message when stores array is empty", () => {
    mockUseWishlistList.mockReturnValue({
      data: { ...pageMeta, items: [] },
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });
    expect(
      screen.getByRole("heading", { name: "Your wishlist is empty" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Browse stores" })).toHaveAttribute(
      "href",
      "/stores",
    );
  });
  it("search toggle reveals the input", async () => {
    mockUseWishlistList.mockReturnValue({
      data: makeData(),
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });

    expect(
      screen.queryByPlaceholderText("Search wishlisted items\u2026"),
    ).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: /search/i }));

    expect(
      screen.getByPlaceholderText("Search wishlisted items\u2026"),
    ).toBeInTheDocument();
  });

  it("asks the server for the typed term", async () => {
    mockUseWishlistList.mockReturnValue({
      data: makeData(),
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });

    await userEvent.click(screen.getByRole("button", { name: /search/i }));
    await userEvent.type(
      screen.getByPlaceholderText("Search wishlisted items\u2026"),
      "skin",
    );

    expect(mockUseWishlistList).toHaveBeenLastCalledWith({ search: "skin" });
  });

  /**
   * A search that matches nothing is not an empty wishlist, and the empty-wishlist message tells
   * the reader to go add items — wrong advice when they have simply mistyped a name.
   */
  it("a search matching nothing does not claim the wishlist is empty", async () => {
    mockUseWishlistList.mockReturnValue({
      data: { ...pageMeta, items: [] },
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });

    await userEvent.click(screen.getByRole("button", { name: /search/i }));
    await userEvent.type(
      screen.getByPlaceholderText("Search wishlisted items\u2026"),
      "nothing",
    );

    expect(
      screen.getByText("Nothing on your wishlist matches \u201cnothing\u201d."),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Your wishlist is empty" }),
    ).not.toBeInTheDocument();
  });

  it("clear search empties the search box and asks for the whole wishlist", async () => {
    mockUseWishlistList.mockReturnValue({
      data: { ...pageMeta, items: [] },
      status: "ready",
      reload: vi.fn(),
    });
    renderWithToasts(<WishlistPage />, { route: "/wishlist" });

    await userEvent.click(screen.getByRole("button", { name: /search/i }));
    const input = screen.getByPlaceholderText("Search wishlisted items\u2026");
    await userEvent.type(input, "nothing");
    await userEvent.click(screen.getByRole("button", { name: "Clear search" }));

    expect(input).toHaveValue("");
    expect(mockUseWishlistList).toHaveBeenLastCalledWith({ search: "" });
  });
});
