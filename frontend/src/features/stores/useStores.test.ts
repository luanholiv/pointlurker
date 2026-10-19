import { act, renderHook, waitFor } from "@testing-library/react";
import { ApiError } from "../../lib/apiClient";
import {
  useStoreLookup,
  useCreateStore,
  useDeactivateStore,
  useRemoveStore,
  useStoreDetail,
  useStoreList,
} from "./useStores";

vi.mock("./storesApi", () => ({
  fetchStores: vi.fn(),
  fetchStoreSummary: vi.fn(),
  fetchStoreDetail: vi.fn(),
  createStore: vi.fn(),
  deactivateStore: vi.fn(),
  removeStore: vi.fn(),
  lookupStore: vi.fn(),
}));

import {
  createStore,
  deactivateStore,
  fetchStoreDetail,
  fetchStores,
  fetchStoreSummary,
  lookupStore,
  removeStore,
} from "./storesApi";

const mockFetchStores = vi.mocked(fetchStores);
const mockFetchStoreSummary = vi.mocked(fetchStoreSummary);
const mockFetchStoreDetail = vi.mocked(fetchStoreDetail);
const mockCreateStore = vi.mocked(createStore);
const mockDeactivateStore = vi.mocked(deactivateStore);
const mockRemoveStore = vi.mocked(removeStore);
const mockLookupStore = vi.mocked(lookupStore);

const storeItem = {
  id: "1",
  channelId: "ch1",
  displayName: "Store",
  isActive: true,
  lastSyncedAt: null,
  itemCount: 0,
  provider: "streamelements" as const,
};

const storeDetail = {
  id: "store-1",
  channelId: "ch1",
  displayName: "Store",
  isActive: true,
  lastSyncedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  itemCount: 0,
  provider: "streamelements" as const,
  missingLabels: [],
};

beforeEach(() => {
  // No `clearMocks` in the vitest config, so call counts and queued `...Once` values would
  // otherwise carry between tests — which matters here because some assert how many requests a
  // hook made.
  vi.resetAllMocks();
  mockFetchStoreSummary.mockResolvedValue({ activeCount: 0, pausedCount: 0 });
});

/** Wraps rows in the shared page envelope the endpoint now returns. */
const page = <T>(items: T[]) => ({
  items,
  totalCount: items.length,
  offset: 0,
  limit: 200,
});

describe("useStoreList", () => {
  it("starts loading then transitions to ready with stores", async () => {
    mockFetchStores.mockResolvedValue(page([storeItem]));

    const { result } = renderHook(() => useStoreList());

    expect(result.current.status).toBe("loading");
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.stores).toHaveLength(1);
    expect(result.current.stores[0].id).toBe("1");
  });

  it("transitions to error on fetch failure", async () => {
    mockFetchStores.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useStoreList());

    await waitFor(() => expect(result.current.status).toBe("error"));
  });

  it("reload refetches and updates stores", async () => {
    mockFetchStores
      .mockResolvedValueOnce(page([]))
      .mockResolvedValueOnce(page([storeItem]));

    const { result } = renderHook(() => useStoreList());
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.stores).toHaveLength(0);

    await act(async () => {
      await result.current.reload();
    });

    expect(result.current.stores).toHaveLength(1);
  });

  it("reload transitions to error on failure", async () => {
    mockFetchStores
      .mockResolvedValueOnce(page([]))
      .mockRejectedValueOnce(new Error("Reload failed"));

    const { result } = renderHook(() => useStoreList());
    await waitFor(() => expect(result.current.status).toBe("ready"));

    await act(async () => {
      await result.current.reload();
    });

    expect(result.current.status).toBe("error");
  });

  it("ignores fetch result when unmounted before resolve", async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let resolveFetch!: (data: any) => void;
    mockFetchStores.mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve;
      }),
    );

    const { unmount } = renderHook(() => useStoreList());
    unmount();

    await act(async () => {
      resolveFetch([]);
    });
  });

  it("ignores fetch error when unmounted before reject", async () => {
    let rejectFetch!: (err: Error) => void;
    mockFetchStores.mockReturnValue(
      new Promise((_, reject) => {
        rejectFetch = reject;
      }),
    );

    const { unmount } = renderHook(() => useStoreList());
    unmount();

    await act(async () => {
      rejectFetch(new Error("Too late"));
    });
  });

  /**
   * The headline counts come from their own endpoint rather than from the rows, so they have to be
   * refetched deliberately instead of updating for free on any refetch.
   * `StoresPage` calls `reload()` after a toggle, a remove and a create, and feeds
   * `activeCount + pausedCount` to the "N / M watched" label — so a summary that never refreshes
   * renders "1 / 0" the moment the first store is added.
   */
  it("reload refetches the headline counts, not just the list", async () => {
    mockFetchStores.mockResolvedValue(page([storeItem]));
    mockFetchStoreSummary
      .mockResolvedValueOnce({ activeCount: 0, pausedCount: 0 })
      .mockResolvedValueOnce({ activeCount: 1, pausedCount: 0 });

    const { result } = renderHook(() => useStoreList());
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.summary.activeCount).toBe(0);

    await act(async () => {
      await result.current.reload();
    });

    expect(result.current.summary.activeCount).toBe(1);
  });

  /**
   * The search box sets `q` straight from `onChange`, so without a debounce every character is its
   * own `ILIKE` query. The item grid already debounces; this is the same box on a different list.
   */
  it("debounces the search term into a single request", async () => {
    vi.useFakeTimers();
    try {
      mockFetchStores.mockResolvedValue(page([]));

      const { rerender } = renderHook(
        ({ search }) => useStoreList({ search }),
        { initialProps: { search: "" } },
      );

      await act(async () => {});
      expect(mockFetchStores).toHaveBeenCalledTimes(1);

      for (const search of ["a", "ab", "abc"]) {
        rerender({ search });
        await act(async () => {
          vi.advanceTimersByTime(50);
        });
      }

      // Still nothing: the burst has not settled.
      expect(mockFetchStores).toHaveBeenCalledTimes(1);

      await act(async () => {
        vi.advanceTimersByTime(300);
      });

      // One request for the term the user stopped on, not one per keystroke.
      expect(mockFetchStores).toHaveBeenCalledTimes(2);
      expect(mockFetchStores.mock.calls.at(-1)?.[0]?.get("q")).toBe("abc");
    } finally {
      vi.useRealTimers();
    }
  });

  /**
   * `reload` and the fetching effect issue the same request, and both have to guard against a late
   * response: a reload started under one filter could otherwise land after — and overwrite — the
   * rows of a newer one.
   */
  it("a superseded reload cannot overwrite newer rows", async () => {
    const storeA = { ...storeItem, id: "a" };
    const storeB = { ...storeItem, id: "b" };

    let resolveReload!: (
      value: ReturnType<typeof page<typeof storeItem>>,
    ) => void;
    const pendingReload = new Promise<
      ReturnType<typeof page<typeof storeItem>>
    >((resolve) => {
      resolveReload = resolve;
    });

    mockFetchStores
      .mockResolvedValueOnce(page([storeA]))
      .mockReturnValueOnce(pendingReload)
      .mockResolvedValueOnce(page([storeB]));

    const { result, rerender } = renderHook(
      ({ active }: { active: boolean }) => useStoreList({ active }),
      { initialProps: { active: true } },
    );
    await waitFor(() => expect(result.current.stores[0]?.id).toBe("a"));

    let reloadPromise!: Promise<void>;
    act(() => {
      reloadPromise = result.current.reload();
    });

    // The filter moves on while that reload is still in flight.
    rerender({ active: false });
    await waitFor(() => expect(result.current.stores[0]?.id).toBe("b"));

    await act(async () => {
      resolveReload(page([storeA]));
      await reloadPromise;
    });

    // The newer filter's rows survive; the stale reload is dropped.
    expect(result.current.stores[0]?.id).toBe("b");
  });
});

describe("useStoreDetail", () => {
  it("starts loading then transitions to ready", async () => {
    mockFetchStoreDetail.mockResolvedValue(storeDetail as never);

    const { result } = renderHook(() => useStoreDetail("store-1"));

    expect(result.current.status).toBe("loading");
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.store).toEqual(storeDetail);
  });

  // The endpoint answers a missing store with problem+json, so what the hook actually receives is
  // an ApiError whose message is the localizable title — no "404" anywhere in the text. Detection
  // reads the status off the contract instead of sniffing the message.
  it("transitions to not_found on a 404 ApiError", async () => {
    mockFetchStoreDetail.mockRejectedValue(
      new ApiError({
        code: "stores.not_found",
        status: 404,
        title: "Store not found.",
      }),
    );

    const { result } = renderHook(() => useStoreDetail("missing"));

    await waitFor(() => expect(result.current.status).toBe("not_found"));
  });

  it("treats a bodyless 404 as a plain error", async () => {
    mockFetchStoreDetail.mockRejectedValue(
      new Error("Request failed with status 404"),
    );

    const { result } = renderHook(() => useStoreDetail("missing"));

    await waitFor(() => expect(result.current.status).toBe("error"));
  });

  it("transitions to error on non-404 failure", async () => {
    mockFetchStoreDetail.mockRejectedValue(new Error("Network error"));

    const { result } = renderHook(() => useStoreDetail("store-1"));

    await waitFor(() => expect(result.current.status).toBe("error"));
  });

  it("ignores fetch result when unmounted before resolve", async () => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    let resolveFetch!: (data: any) => void;
    mockFetchStoreDetail.mockReturnValue(
      new Promise((resolve) => {
        resolveFetch = resolve;
      }),
    );

    const { unmount } = renderHook(() => useStoreDetail("store-1"));
    unmount();

    await act(async () => {
      resolveFetch(storeDetail);
    });
  });

  it("ignores fetch error when unmounted before reject", async () => {
    let rejectFetch!: (err: Error) => void;
    mockFetchStoreDetail.mockReturnValue(
      new Promise((_, reject) => {
        rejectFetch = reject;
      }),
    );

    const { unmount } = renderHook(() => useStoreDetail("store-1"));
    unmount();

    await act(async () => {
      rejectFetch(new Error("Too late"));
    });
  });
});

describe("useCreateStore", () => {
  it("execute transitions saving → idle and returns result on success", async () => {
    mockCreateStore.mockResolvedValue(storeDetail as never);

    const { result } = renderHook(() => useCreateStore());

    let returnValue: unknown;
    await act(async () => {
      returnValue = await result.current.execute({
        provider: "streamelements",
        channelId: "ch1",
        displayName: "Store",
      });
    });

    expect(returnValue).toEqual(storeDetail);
    expect(result.current.status).toBe("idle");
  });

  it("execute transitions saving → error on failure", async () => {
    mockCreateStore.mockRejectedValue(new Error("Already exists"));

    const { result } = renderHook(() => useCreateStore());

    await act(async () => {
      await result.current.execute({
        provider: "streamelements",
        channelId: "ch1",
        displayName: "Store",
      });
    });

    expect(result.current.status).toBe("error");
    // The rejection's own message is developer copy — the user reads the fallback key.
    expect(result.current.errorMessage).toBe("Failed to create store");
  });

  it("resolves the backend code through the catalog", async () => {
    mockCreateStore.mockRejectedValue(
      new ApiError({
        code: "stores.already_exists",
        params: { channelId: "ch1" },
        status: 409,
        title: "Store already exists",
      }),
    );

    const { result } = renderHook(() => useCreateStore());

    await act(async () => {
      await result.current.execute({
        provider: "streamelements",
        channelId: "ch1",
        displayName: "Store",
      });
    });

    expect(result.current.errorMessage).toBe(
      "A store for channel 'ch1' already exists.",
    );
  });

  it("execute returns null on failure", async () => {
    mockCreateStore.mockRejectedValue(new Error("Failed"));

    const { result } = renderHook(() => useCreateStore());

    let returnValue: unknown = "sentinel";
    await act(async () => {
      returnValue = await result.current.execute({
        provider: "streamelements",
        channelId: "ch1",
        displayName: "Store",
      });
    });

    expect(returnValue).toBeNull();
  });

  it("uses fallback message when error is not an Error instance", async () => {
    mockCreateStore.mockRejectedValue("string error");

    const { result } = renderHook(() => useCreateStore());

    await act(async () => {
      await result.current.execute({
        provider: "streamelements",
        channelId: "ch1",
        displayName: "Store",
      });
    });

    expect(result.current.errorMessage).toBe("Failed to create store");
  });
});

describe("useDeactivateStore", () => {
  it("execute returns true on success", async () => {
    mockDeactivateStore.mockResolvedValue(undefined as never);

    const { result } = renderHook(() => useDeactivateStore());

    let returned: boolean | undefined;
    await act(async () => {
      returned = await result.current.execute("store-1");
    });

    expect(returned).toBe(true);
    expect(result.current.status).toBe("idle");
  });

  it("execute returns false and sets error on failure", async () => {
    mockDeactivateStore.mockRejectedValue(new Error("Failed"));

    const { result } = renderHook(() => useDeactivateStore());

    let returned: boolean | undefined;
    await act(async () => {
      returned = await result.current.execute("store-1");
    });

    expect(returned).toBe(false);
    expect(result.current.status).toBe("error");
  });
});

describe("useRemoveStore", () => {
  it("execute returns true on success", async () => {
    mockRemoveStore.mockResolvedValue(undefined as never);

    const { result } = renderHook(() => useRemoveStore());

    let returned: boolean | undefined;
    await act(async () => {
      returned = await result.current.execute("store-1");
    });

    expect(returned).toBe(true);
    expect(result.current.status).toBe("idle");
  });

  it("execute returns false on failure", async () => {
    mockRemoveStore.mockRejectedValue(new Error("Failed"));

    const { result } = renderHook(() => useRemoveStore());

    let returned: boolean | undefined;
    await act(async () => {
      returned = await result.current.execute("store-1");
    });

    expect(returned).toBe(false);
  });
});

const seLookup = {
  externalId: "ch1",
  handle: "xqc",
  displayName: "xQc",
  avatarUrl: null,
  itemCount: null,
};

describe("useStoreLookup", () => {
  it("initial state is idle", () => {
    const { result } = renderHook(() => useStoreLookup());
    expect(result.current.state.status).toBe("idle");
  });

  it("lookup transitions idle → resolved on success", async () => {
    mockLookupStore.mockResolvedValue(seLookup);

    const { result } = renderHook(() => useStoreLookup());

    await act(async () => {
      await result.current.lookup("streamelements", "xqc");
    });

    expect(mockLookupStore).toHaveBeenCalledWith("streamelements", "xqc");
    expect(result.current.state).toEqual({
      status: "resolved",
      provider: "streamelements",
      externalId: "ch1",
      displayName: "xQc",
      itemCount: null,
    });
  });

  it("a fixed-store lookup sends no query and carries the item count", async () => {
    mockLookupStore.mockResolvedValue({
      externalId: "gaupoints",
      handle: "gaupoints",
      displayName: "GauPoints",
      avatarUrl: null,
      itemCount: 12,
    });

    const { result } = renderHook(() => useStoreLookup());

    await act(async () => {
      await result.current.lookup("gaupoints");
    });

    expect(mockLookupStore).toHaveBeenCalledWith("gaupoints", undefined);
    expect(result.current.state).toMatchObject({
      status: "resolved",
      provider: "gaupoints",
      itemCount: 12,
    });
  });

  it("lookup sets 404 error message when no store is found", async () => {
    mockLookupStore.mockRejectedValue(
      new ApiError({
        code: "store_lookup.not_found",
        params: { provider: "streamelements", query: "nobody" },
        status: 404,
        title: "No store found",
      }),
    );

    const { result } = renderHook(() => useStoreLookup());

    await act(async () => {
      await result.current.lookup("streamelements", "nobody");
    });

    expect(result.current.state.status).toBe("error");
    if (result.current.state.status === "error") {
      expect(result.current.state.message).toBe("No store found for 'nobody'.");
    }
  });

  // The backend refuses a blank query through the query-contract validator, so the code arrives
  // nested in `validation.failed` rather than as the response's own code.
  it("lookup sets 400 error message for a missing query", async () => {
    mockLookupStore.mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "query",
              code: "store_lookup.query_required",
              params: { field: "query" },
            },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );

    const { result } = renderHook(() => useStoreLookup());

    await act(async () => {
      await result.current.lookup("streamelements", "");
    });

    expect(result.current.state.status).toBe("error");
    if (result.current.state.status === "error") {
      expect(result.current.state.message).toBe(
        "Enter a channel name to search for.",
      );
    }
  });

  it("lookup sets unreachable message on network error", async () => {
    mockLookupStore.mockRejectedValue(new Error("fetch failed"));

    const { result } = renderHook(() => useStoreLookup());

    await act(async () => {
      await result.current.lookup("streamelements", "xqc");
    });

    expect(result.current.state.status).toBe("error");
    if (result.current.state.status === "error") {
      expect(result.current.state.message).toBe(
        "The store provider is unreachable. Try again later.",
      );
    }
  });

  it("reset returns state to idle from resolved", async () => {
    mockLookupStore.mockResolvedValue(seLookup);

    const { result } = renderHook(() => useStoreLookup());

    await act(async () => {
      await result.current.lookup("streamelements", "xqc");
    });
    expect(result.current.state.status).toBe("resolved");

    act(() => {
      result.current.reset();
    });

    expect(result.current.state.status).toBe("idle");
  });
});
