import { renderHook, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router";
import {
  AuthContext,
  type AuthContextValue,
  type AuthUser,
} from "../auth/AuthContext";
import { useSetupProgress } from "./useSetupProgress";

vi.mock("../stores/storesApi", () => ({
  fetchStoreSummary: vi.fn(),
}));
vi.mock("../wishlist/wishlistApi", () => ({
  fetchWishlistGrouped: vi.fn(),
}));

import { fetchStoreSummary } from "../stores/storesApi";
import { fetchWishlistGrouped } from "../wishlist/wishlistApi";

const mockFetchStoreSummary = vi.mocked(fetchStoreSummary);
const mockFetchWishlistGrouped = vi.mocked(fetchWishlistGrouped);

const baseUser: AuthUser = {
  userId: "user",
  email: "admin@example.com",
  credentials: [],
  twoFactorEnabled: false,
  locale: "en-US",
};

const emptyWishlist = { items: [], totalCount: 0, offset: 0, limit: 50 };

const oneEntryWishlist = {
  items: [
    {
      storeId: "s1",
      storeName: "Store",
      isActive: true,
      lastSyncedAt: null,
      entries: [
        {
          id: "w1",
          storeItemId: "i1",
          itemName: "Item",
          itemCost: 100,
          isAvailable: false,
          notifyOnAvailable: true,
          autoRedeem: false,
          desiredRedemptions: 1,
          quotaUsed: 0,
          isBlocked: false,
          missingLabels: [],
          createdAt: "2026-10-01T00:00:00Z",
        },
      ],
    },
  ],
  totalCount: 1,
  offset: 0,
  limit: 50,
};

const renderProgress = (user: AuthUser | null) => {
  const auth = { user } as AuthContextValue;
  const wrapper = ({ children }: { children: ReactNode }) => (
    <AuthContext.Provider value={auth}>
      <MemoryRouter>{children}</MemoryRouter>
    </AuthContext.Provider>
  );
  return renderHook(() => useSetupProgress(), { wrapper });
};

describe("useSetupProgress", () => {
  beforeEach(() => {
    mockFetchStoreSummary.mockResolvedValue({ activeCount: 0, pausedCount: 0 });
    mockFetchWishlistGrouped.mockResolvedValue(emptyWishlist);
  });

  it("reports loading until both counts arrive", () => {
    mockFetchStoreSummary.mockReturnValue(new Promise(() => {}));
    const { result } = renderProgress(baseUser);
    expect(result.current.status).toBe("loading");
  });

  it("marks nothing done on a fresh install", async () => {
    const { result } = renderProgress(baseUser);
    await waitFor(() => expect(result.current.status).toBe("ready"));

    expect(result.current).toMatchObject({
      done: 0,
      total: 3,
      isComplete: false,
      steps: [
        { id: "credential", done: false },
        { id: "store", done: false },
        { id: "wishlist", done: false },
      ],
    });
  });

  it("counts a provider credential as connected", async () => {
    const { result } = renderProgress({
      ...baseUser,
      credentials: [
        {
          provider: "gaupoints",
          accountName: null,
          expiresAt: null,
          lastValidatedAt: null,
          lastValidationError: null,
        },
      ],
    });
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(
      result.current.status === "ready" && result.current.steps[0].done,
    ).toBe(true);
  });

  it("counts paused stores as added", async () => {
    mockFetchStoreSummary.mockResolvedValue({ activeCount: 0, pausedCount: 1 });
    const { result } = renderProgress(baseUser);
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(
      result.current.status === "ready" && result.current.steps[1].done,
    ).toBe(true);
  });

  it("is complete once all three steps are done", async () => {
    mockFetchStoreSummary.mockResolvedValue({ activeCount: 1, pausedCount: 0 });
    mockFetchWishlistGrouped.mockResolvedValue(oneEntryWishlist);
    const { result } = renderProgress({
      ...baseUser,
      credentials: [
        {
          provider: "streamelements",
          accountName: null,
          expiresAt: null,
          lastValidatedAt: null,
          lastValidationError: null,
        },
      ],
    });
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current).toMatchObject({
      done: 3,
      total: 3,
      isComplete: true,
    });
  });

  it("reports auto-redeem off until an entry has it on", async () => {
    mockFetchWishlistGrouped.mockResolvedValue(oneEntryWishlist);
    const { result } = renderProgress(baseUser);
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current).toMatchObject({ autoRedeem: false });
  });

  it("reports auto-redeem on when any entry has it on, without counting it as a step", async () => {
    const [group] = oneEntryWishlist.items;
    mockFetchWishlistGrouped.mockResolvedValue({
      ...oneEntryWishlist,
      items: [
        {
          ...group,
          entries: [
            group.entries[0],
            { ...group.entries[0], id: "w2", autoRedeem: true },
          ],
        },
      ],
    });
    const { result } = renderProgress(baseUser);
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current).toMatchObject({ autoRedeem: true, total: 3 });
  });

  it("reports an error when a count cannot be read", async () => {
    mockFetchWishlistGrouped.mockRejectedValue(new Error("boom"));
    const { result } = renderProgress(baseUser);
    await waitFor(() => expect(result.current.status).toBe("error"));
  });
});
