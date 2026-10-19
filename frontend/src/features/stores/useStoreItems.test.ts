import { renderHook, act, waitFor } from "@testing-library/react";
import { vi, describe, it, expect, beforeEach, afterEach } from "vitest";
import { useStoreItems } from "./useStoreItems";
import { DEFAULT_FILTERS } from "./types";
import type { StoreItemFilters } from "./types";
import type { Page, StoreItemDetail } from "./storesApi";

const fetchStoreItems = vi.hoisted(() => vi.fn());

vi.mock("./storesApi", async (importOriginal) => ({
  ...(await importOriginal<typeof import("./storesApi")>()),
  fetchStoreItems,
}));

const row = (
  id: string,
  overrides: Partial<StoreItemDetail> = {},
): StoreItemDetail => ({
  id,
  providerItemId: `se-${id}`,
  name: `Item ${id}`,
  description: null,
  cost: 100,
  categoryName: "General",
  subscriberOnly: false,
  isAvailable: true,
  quantityCurrent: 5,
  quantityTotal: 10,
  allowMessages: false,
  thumbnailUrl: null,
  wishlistEntryId: null,
  providerCreatedAt: "2026-01-01T00:00:00Z",
  ...overrides,
});

const page = (
  items: StoreItemDetail[],
  totalCount = items.length,
  offset = 0,
): Page<StoreItemDetail> => ({ items, totalCount, offset, limit: 48 });

const filters = (
  overrides: Partial<StoreItemFilters> = {},
): StoreItemFilters => ({
  ...DEFAULT_FILTERS,
  ...overrides,
});

describe("useStoreItems", () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    fetchStoreItems.mockReset();
    fetchStoreItems.mockResolvedValue(page([row("1"), row("2")]));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("loads the first slice for a store", async () => {
    const { result } = renderHook(() => useStoreItems("store-1", filters()));

    await waitFor(() => expect(result.current.status).toBe("ready"));

    expect(result.current.items.map((i) => i.id)).toEqual(["1", "2"]);
    expect(result.current.totalCount).toBe(2);
  });

  it("sends the filters as the query string the browser URL already uses", async () => {
    renderHook(() =>
      useStoreItems(
        "store-1",
        filters({ search: "gold", sort: "cost", sortDir: "desc" }),
      ),
    );

    await waitFor(() => expect(fetchStoreItems).toHaveBeenCalled());

    const query = fetchStoreItems.mock.calls[0][1] as URLSearchParams;
    expect(query.get("q")).toBe("gold");
    expect(query.get("sort")).toBe("cost");
    expect(query.get("dir")).toBe("desc");
  });

  describe("debounce", () => {
    it("does not issue a request per keystroke", async () => {
      const { rerender } = renderHook(
        ({ f }: { f: StoreItemFilters }) => useStoreItems("store-1", f),
        { initialProps: { f: filters({ search: "g" }) } },
      );

      await waitFor(() => expect(fetchStoreItems).toHaveBeenCalledTimes(1));

      rerender({ f: filters({ search: "go" }) });
      rerender({ f: filters({ search: "gol" }) });
      rerender({ f: filters({ search: "gold" }) });

      // Nothing more has gone out while the term is still settling.
      expect(fetchStoreItems).toHaveBeenCalledTimes(1);

      await act(async () => {
        await vi.advanceTimersByTimeAsync(400);
      });

      await waitFor(() => expect(fetchStoreItems).toHaveBeenCalledTimes(2));
      const query = fetchStoreItems.mock.calls[1][1] as URLSearchParams;
      expect(query.get("q")).toBe("gold");
    });

    it("applies a non-text filter immediately", async () => {
      const { rerender } = renderHook(
        ({ f }: { f: StoreItemFilters }) => useStoreItems("store-1", f),
        { initialProps: { f: filters() } },
      );

      await waitFor(() => expect(fetchStoreItems).toHaveBeenCalledTimes(1));

      // A checkbox is a discrete decision; debouncing it would read as lag.
      rerender({ f: filters({ featured: true }) });

      await waitFor(() => expect(fetchStoreItems).toHaveBeenCalledTimes(2));
    });
  });

  it("aborts a superseded request", async () => {
    const { rerender } = renderHook(
      ({ f }: { f: StoreItemFilters }) => useStoreItems("store-1", f),
      { initialProps: { f: filters() } },
    );

    await waitFor(() => expect(fetchStoreItems).toHaveBeenCalledTimes(1));
    const firstSignal = fetchStoreItems.mock.calls[0][2] as AbortSignal;

    rerender({ f: filters({ featured: true }) });

    await waitFor(() => expect(firstSignal.aborted).toBe(true));
  });

  it("keeps the previous items visible while the next query loads", async () => {
    let release: ((value: Page<StoreItemDetail>) => void) | undefined;
    fetchStoreItems.mockResolvedValueOnce(page([row("1")]));

    const { result, rerender } = renderHook(
      ({ f }: { f: StoreItemFilters }) => useStoreItems("store-1", f),
      { initialProps: { f: filters() } },
    );

    await waitFor(() => expect(result.current.items).toHaveLength(1));

    fetchStoreItems.mockImplementationOnce(
      () =>
        new Promise<Page<StoreItemDetail>>((resolve) => (release = resolve)),
    );
    rerender({ f: filters({ featured: true }) });

    // The grid does not blank: this is the regression the redemption page still has.
    await waitFor(() => expect(result.current.isRefreshing).toBe(true));
    expect(result.current.status).toBe("ready");
    expect(result.current.items).toHaveLength(1);

    await act(async () => {
      release!(page([row("9")]));
    });

    await waitFor(() =>
      expect(result.current.items.map((i) => i.id)).toEqual(["9"]),
    );
  });

  it("does not let a late response overwrite a newer one", async () => {
    let releaseSlow: ((value: Page<StoreItemDetail>) => void) | undefined;

    fetchStoreItems.mockImplementationOnce(
      () =>
        new Promise<Page<StoreItemDetail>>(
          (resolve) => (releaseSlow = resolve),
        ),
    );

    const { result, rerender } = renderHook(
      ({ f }: { f: StoreItemFilters }) => useStoreItems("store-1", f),
      { initialProps: { f: filters() } },
    );

    await waitFor(() => expect(fetchStoreItems).toHaveBeenCalledTimes(1));

    fetchStoreItems.mockResolvedValueOnce(page([row("newer")]));
    rerender({ f: filters({ featured: true }) });

    await waitFor(() =>
      expect(result.current.items.map((i) => i.id)).toEqual(["newer"]),
    );

    // The first request finally answers — with what is now stale data.
    await act(async () => {
      releaseSlow!(page([row("older")]));
    });

    expect(result.current.items.map((i) => i.id)).toEqual(["newer"]);
  });

  describe("load more", () => {
    it("appends the next slice", async () => {
      fetchStoreItems.mockResolvedValueOnce(page([row("1"), row("2")], 4));

      const { result } = renderHook(() => useStoreItems("store-1", filters()));
      await waitFor(() => expect(result.current.items).toHaveLength(2));

      fetchStoreItems.mockResolvedValueOnce(page([row("3"), row("4")], 4, 2));
      await act(async () => {
        result.current.loadMore();
      });

      await waitFor(() =>
        expect(result.current.items.map((i) => i.id)).toEqual([
          "1",
          "2",
          "3",
          "4",
        ]),
      );
      expect(result.current.hasMore).toBe(false);
    });

    it("requests the next slice from the accumulated length", async () => {
      fetchStoreItems.mockResolvedValueOnce(page([row("1"), row("2")], 10));

      const { result } = renderHook(() => useStoreItems("store-1", filters()));
      await waitFor(() => expect(result.current.items).toHaveLength(2));

      await act(async () => {
        result.current.loadMore();
      });

      await waitFor(() => expect(fetchStoreItems).toHaveBeenCalledTimes(2));
      const query = fetchStoreItems.mock.calls[1][1] as URLSearchParams;
      expect(query.get("offset")).toBe("2");
    });

    it("resets the accumulator when a filter changes", async () => {
      fetchStoreItems.mockResolvedValueOnce(page([row("1"), row("2")], 10));

      const { result, rerender } = renderHook(
        ({ f }: { f: StoreItemFilters }) => useStoreItems("store-1", f),
        { initialProps: { f: filters() } },
      );
      await waitFor(() => expect(result.current.items).toHaveLength(2));

      fetchStoreItems.mockResolvedValueOnce(page([row("only")], 1));
      rerender({ f: filters({ featured: true }) });

      await waitFor(() =>
        expect(result.current.items.map((i) => i.id)).toEqual(["only"]),
      );
    });
  });

  describe("mid-scroll drift", () => {
    it("renders a repeated item once", async () => {
      fetchStoreItems.mockResolvedValueOnce(page([row("1"), row("2")], 4));

      const { result } = renderHook(() => useStoreItems("store-1", filters()));
      await waitFor(() => expect(result.current.items).toHaveLength(2));

      // A sync shifted the result set between slices, so "2" arrives twice.
      fetchStoreItems.mockResolvedValueOnce(page([row("2"), row("3")], 4, 2));
      await act(async () => {
        result.current.loadMore();
      });

      await waitFor(() =>
        expect(result.current.items.map((i) => i.id)).toEqual(["1", "2", "3"]),
      );
    });

    it("stops requesting when a slice contributes nothing new", async () => {
      fetchStoreItems.mockResolvedValueOnce(page([row("1"), row("2")], 10));

      const { result } = renderHook(() => useStoreItems("store-1", filters()));
      await waitFor(() => expect(result.current.items).toHaveLength(2));

      // Everything in this slice is already accumulated.
      fetchStoreItems.mockResolvedValueOnce(page([row("1"), row("2")], 10, 2));
      await act(async () => {
        result.current.loadMore();
      });

      await waitFor(() => expect(result.current.hasMore).toBe(false));
    });

    /**
     * If a row is skipped, the accumulated count can never reach the reported total. A done-check
     * written only against the count would leave a "Load more" that returns nothing forever.
     */
    it("terminates even when the count can no longer reach the total", async () => {
      fetchStoreItems.mockResolvedValueOnce(page([row("1"), row("2")], 10));

      const { result } = renderHook(() => useStoreItems("store-1", filters()));
      await waitFor(() => expect(result.current.items).toHaveLength(2));

      fetchStoreItems.mockResolvedValueOnce(page([], 10, 2));
      await act(async () => {
        result.current.loadMore();
      });

      await waitFor(() => expect(result.current.hasMore).toBe(false));
      expect(result.current.items).toHaveLength(2);
      expect(result.current.totalCount).toBe(10);
    });
  });

  it("reports an error without discarding what is already shown", async () => {
    fetchStoreItems.mockResolvedValueOnce(page([row("1")], 5));

    const { result } = renderHook(() => useStoreItems("store-1", filters()));
    await waitFor(() => expect(result.current.items).toHaveLength(1));

    fetchStoreItems.mockRejectedValueOnce(new Error("boom"));
    await act(async () => {
      result.current.loadMore();
    });

    await waitFor(() => expect(result.current.status).toBe("error"));
    expect(result.current.items).toHaveLength(1);
  });
});
