import { describe, it, expect, beforeEach } from "vitest";
import { act, renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import {
  useWishlistList,
  useAddWishlist,
  useUpdateWishlist,
  useRemoveWishlist,
  useWishlistEntryDetail,
} from "./useWishlist";
import { setAccessToken, clearAccessToken } from "../auth/authStorage";

/** Only `items` varies between these fixtures, so the rest of the envelope is spread in. */
const pageMeta = { totalCount: 0, offset: 0, limit: 0 };

/**
 * A backend error the way `apiClient` recognizes one: only `application/problem+json` is parsed
 * into an `ApiError` carrying `code`, so a plain JSON body of the same shape would be discarded
 * and land on the generic fallback instead.
 */
const problem = (status: number, body: Record<string, unknown>) =>
  HttpResponse.json(body, {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });

describe("useWishlist hooks", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  afterEach(() => {
    clearAccessToken();
  });

  describe("useWishlistList", () => {
    it("starts in loading state and transitions to ready", async () => {
      const { result } = renderHook(() => useWishlistList());
      expect(result.current.status).toBe("loading");
      await waitFor(() => expect(result.current.status).toBe("ready"));
      expect(result.current.data).not.toBeNull();
      expect(result.current.data!.items).toEqual([]);
    });

    it("transitions to error state when API fails", async () => {
      server.use(
        http.get(
          "http://localhost/api/wishlist",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      const { result } = renderHook(() => useWishlistList());
      await waitFor(() => expect(result.current.status).toBe("error"));
      expect(result.current.data).toBeNull();
    });

    it("returns store data when API responds with stores", async () => {
      server.use(
        http.get("http://localhost/api/wishlist", () =>
          HttpResponse.json({
            ...pageMeta,
            items: [
              {
                storeId: "store-1",
                storeName: "xQc Store",
                isActive: true,
                lastSyncedAt: null,
                entries: [],
              },
            ],
          }),
        ),
      );
      const { result } = renderHook(() => useWishlistList());
      await waitFor(() => expect(result.current.status).toBe("ready"));
      expect(result.current.data!.items).toHaveLength(1);
      expect(result.current.data!.items[0].storeName).toBe("xQc Store");
    });
  });

  describe("useWishlistList — refreshing without blanking", () => {
    const group = (storeId: string) => ({
      storeId,
      storeName: storeId,
      isActive: true,
      lastSyncedAt: null,
      entries: [],
    });

    /**
     * `WishlistPage` renders the list only while `status === "ready"`, so dropping back to
     * "loading" on a reload replaces the whole list with a loading line — after every add, edit and
     * remove. The redemption history had the same regression and was fixed the same way.
     */
    it("keeps the previous groups on screen while a reload is in flight", async () => {
      server.use(
        http.get("http://localhost/api/wishlist", () =>
          HttpResponse.json({ ...pageMeta, items: [group("store-1")] }),
        ),
      );

      const { result } = renderHook(() => useWishlistList());
      await waitFor(() => expect(result.current.status).toBe("ready"));

      let release!: () => void;
      const gate = new Promise<void>((resolve) => {
        release = resolve;
      });
      server.use(
        http.get("http://localhost/api/wishlist", async () => {
          await gate;
          return HttpResponse.json({ ...pageMeta, items: [group("store-2")] });
        }),
      );

      act(() => {
        void result.current.reload();
      });

      // Still showing the old groups, not a loading state.
      expect(result.current.status).toBe("ready");
      expect(result.current.data!.items[0].storeId).toBe("store-1");

      await act(async () => {
        release();
      });
      await waitFor(() =>
        expect(result.current.data!.items[0].storeId).toBe("store-2"),
      );
    });

    /**
     * The search box sets the term straight from `onChange`, so without a debounce every character
     * is its own query.
     */
    it("debounces the search term into a single request", async () => {
      const terms: (string | null)[] = [];

      server.use(
        http.get("http://localhost/api/wishlist", ({ request }) => {
          terms.push(new URL(request.url).searchParams.get("q"));
          return HttpResponse.json({ ...pageMeta, items: [] });
        }),
      );

      const { rerender } = renderHook(
        ({ search }: { search: string }) => useWishlistList({ search }),
        { initialProps: { search: "" } },
      );
      await waitFor(() => expect(terms).toEqual([null]));

      // One synchronous burst, so no debounce window elapses between the keystrokes.
      rerender({ search: "a" });
      rerender({ search: "ab" });
      rerender({ search: "abc" });
      expect(terms).toEqual([null]);

      await waitFor(() => expect(terms).toHaveLength(2), { timeout: 2000 });
      expect(terms.at(-1)).toBe("abc");
    });

    /**
     * `reload` and the fetching effect issued the same request through two code paths and only the
     * effect guarded against a late response, so a reload begun under one filter could land after —
     * and overwrite — the results of a newer one.
     */
    it("a superseded reload cannot overwrite newer groups", async () => {
      let unfilteredCalls = 0;
      let release!: () => void;
      const gate = new Promise<void>((resolve) => {
        release = resolve;
      });

      server.use(
        http.get("http://localhost/api/wishlist", async ({ request }) => {
          if (new URL(request.url).searchParams.get("avail") === "1") {
            return HttpResponse.json({
              ...pageMeta,
              items: [group("store-filtered")],
            });
          }

          unfilteredCalls += 1;
          // The second unfiltered request is the reload; hold it open.
          if (unfilteredCalls === 2) {
            await gate;
          }
          return HttpResponse.json({
            ...pageMeta,
            items: [group("store-unfiltered")],
          });
        }),
      );

      const { result, rerender } = renderHook(
        ({ availableOnly }: { availableOnly: boolean }) =>
          useWishlistList({ availableOnly }),
        { initialProps: { availableOnly: false } },
      );
      await waitFor(() =>
        expect(result.current.data?.items[0]?.storeId).toBe("store-unfiltered"),
      );

      act(() => {
        void result.current.reload();
      });

      // The filter moves on while that reload is still open.
      rerender({ availableOnly: true });
      await waitFor(() =>
        expect(result.current.data?.items[0]?.storeId).toBe("store-filtered"),
      );

      await act(async () => {
        release();
      });

      expect(result.current.data?.items[0]?.storeId).toBe("store-filtered");
    });
  });

  describe("useAddWishlist", () => {
    it("starts in idle state", () => {
      const { result } = renderHook(() => useAddWishlist());
      expect(result.current.status).toBe("idle");
    });

    it("transitions saving → idle and returns result on success", async () => {
      const { result } = renderHook(() => useAddWishlist());
      const returned = await result.current.execute({
        storeItemId: "item-1",
        notifyOnAvailable: true,
        autoRedeem: false,
      });
      await waitFor(() => expect(result.current.status).toBe("idle"));
      expect(returned).not.toBeNull();
    });

    it("transitions to error state and returns null on failure", async () => {
      server.use(
        http.post(
          "http://localhost/api/wishlist",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      const { result } = renderHook(() => useAddWishlist());
      const returned = await result.current.execute({
        storeItemId: "item-1",
        notifyOnAvailable: false,
        autoRedeem: false,
      });
      await waitFor(() => expect(result.current.status).toBe("error"));
      expect(returned).toBeNull();
      expect(result.current.errorMessage).toBe("Failed to add to wishlist");
    });

    it("carries the backend's reason when the failure has a code", async () => {
      server.use(
        http.post("http://localhost/api/wishlist", () =>
          problem(409, { code: "wishlist.already_exists" }),
        ),
      );
      const { result } = renderHook(() => useAddWishlist());
      await result.current.execute({
        storeItemId: "item-1",
        notifyOnAvailable: false,
        autoRedeem: false,
      });
      await waitFor(() =>
        expect(result.current.errorMessage).toBe(
          "This item is already in your wishlist.",
        ),
      );
    });
  });

  describe("useUpdateWishlist", () => {
    it("returns true on success", async () => {
      const { result } = renderHook(() => useUpdateWishlist());
      const returned = await result.current.execute("entry-1", {
        notifyOnAvailable: false,
        autoRedeem: false,
      });
      await waitFor(() => expect(result.current.status).toBe("idle"));
      expect(returned).toBe(true);
    });

    it("returns false and sets error state on failure", async () => {
      server.use(
        http.put(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      const { result } = renderHook(() => useUpdateWishlist());
      const returned = await result.current.execute("entry-1", {
        notifyOnAvailable: false,
        autoRedeem: false,
      });
      await waitFor(() => expect(result.current.status).toBe("error"));
      expect(returned).toBe(false);
      expect(result.current.errorMessage).toBe(
        "Failed to update wishlist entry",
      );
    });

    it("carries the backend's reason when the failure has a code", async () => {
      // The shape the request-validation filter actually returns: a validation.failed wrapper
      // whose first per-field entry names the rule that broke.
      server.use(
        http.put("http://localhost/api/wishlist/:id", () =>
          problem(400, {
            code: "validation.failed",
            params: {
              errors: [
                {
                  field: "redemptionMessage",
                  code: "validation.too_long",
                  params: { field: "redemptionMessage", max: 200 },
                },
              ],
            },
          }),
        ),
      );
      const { result } = renderHook(() => useUpdateWishlist());
      await result.current.execute("entry-1", {
        notifyOnAvailable: false,
        autoRedeem: false,
      });
      await waitFor(() =>
        expect(result.current.errorMessage).toBe(
          "Value is too long (maximum 200 characters).",
        ),
      );
    });
  });

  describe("useRemoveWishlist", () => {
    it("reports success", async () => {
      const { result } = renderHook(() => useRemoveWishlist());
      const outcome = await result.current.execute("entry-1");
      await waitFor(() => expect(result.current.status).toBe("idle"));
      expect(outcome.ok).toBe(true);
    });

    it("reports failure", async () => {
      server.use(
        http.delete(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      const { result } = renderHook(() => useRemoveWishlist());
      const outcome = await result.current.execute("entry-1");
      await waitFor(() => expect(result.current.status).toBe("error"));
      expect(outcome.ok).toBe(false);
    });

    // A bare `false` discards the reason the backend gave, and tells the user "something failed"
    // for a cause the server had already named.
    it("carries the backend's reason on the failure branch", async () => {
      server.use(
        http.delete("http://localhost/api/wishlist/:id", () =>
          problem(404, { code: "wishlist.entry_not_found" }),
        ),
      );
      const { result } = renderHook(() => useRemoveWishlist());
      const outcome = await result.current.execute("entry-1");
      expect(outcome).toEqual({
        ok: false,
        message: "Wishlist entry not found.",
      });
    });

    it("falls back to a catalog string when the remove failure carries no code", async () => {
      server.use(
        http.delete(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      const { result } = renderHook(() => useRemoveWishlist());
      const outcome = await result.current.execute("entry-1");
      expect(outcome).toEqual({
        ok: false,
        message: "Failed to remove wishlist entry",
      });
    });
  });

  describe("useWishlistEntryDetail", () => {
    it("returns the entry and stays idle on success", async () => {
      const { result } = renderHook(() => useWishlistEntryDetail());
      const outcome = await result.current.execute("entry-1");
      await waitFor(() => expect(result.current.status).toBe("idle"));
      expect(outcome.ok && outcome.entry.id).toBe("entry-1");
    });

    // The message is asserted off the resolved value, not off hook state: a handler awaiting
    // execute cannot see a state update made during its own call.
    it("carries the backend's reason on a coded failure", async () => {
      server.use(
        http.get("http://localhost/api/wishlist/:id", () =>
          problem(404, { code: "wishlist.entry_not_found" }),
        ),
      );
      const { result } = renderHook(() => useWishlistEntryDetail());
      const outcome = await result.current.execute("entry-1");
      await waitFor(() => expect(result.current.status).toBe("error"));
      expect(outcome).toEqual({
        ok: false,
        message: "Wishlist entry not found.",
      });
    });

    it("falls back to a catalog string when the response carries no code", async () => {
      server.use(
        http.get(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      const { result } = renderHook(() => useWishlistEntryDetail());
      const outcome = await result.current.execute("entry-1");
      expect(outcome).toEqual({
        ok: false,
        message: "Failed to load wishlist entry",
      });
    });

    // Retrying is the obvious response to a transient failure, so the second attempt must not
    // inherit anything from the first.
    it("recovers when a retry succeeds", async () => {
      server.use(
        http.get(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      const { result } = renderHook(() => useWishlistEntryDetail());
      expect((await result.current.execute("entry-1")).ok).toBe(false);
      await waitFor(() => expect(result.current.status).toBe("error"));

      server.resetHandlers();
      const outcome = await result.current.execute("entry-1");

      await waitFor(() => expect(result.current.status).toBe("idle"));
      expect(outcome.ok && outcome.entry.id).toBe("entry-1");
    });
  });
});
