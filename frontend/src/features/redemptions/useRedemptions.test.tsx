import { describe, it, expect, beforeEach } from "vitest";
import { renderHook, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import { useRedemptions } from "./useRedemptions";
import { setAccessToken, clearAccessToken } from "../auth/authStorage";
import type { RedemptionFilter } from "./redemptionsApi";

describe("useRedemptions", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  afterEach(() => {
    clearAccessToken();
  });

  it("starts in loading state and transitions to ready", async () => {
    const { result } = renderHook(() => useRedemptions({}));
    expect(result.current.status).toBe("loading");
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.data).not.toBeNull();
  });

  it("returns empty items list from default handler", async () => {
    const { result } = renderHook(() => useRedemptions({}));
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.data!.items).toEqual([]);
    expect(result.current.data!.totalCount).toBe(0);
  });

  it("transitions to error state when API fails", async () => {
    server.use(
      http.get(
        "http://localhost/api/redemptions",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    const { result } = renderHook(() => useRedemptions({}));
    await waitFor(() => expect(result.current.status).toBe("error"));
    expect(result.current.data).toBeNull();
  });

  it("re-fetches when filter changes", async () => {
    let callCount = 0;
    server.use(
      http.get("http://localhost/api/redemptions", () => {
        callCount++;
        return HttpResponse.json({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 20,
        });
      }),
    );

    let filter: RedemptionFilter = {};
    const { result, rerender } = renderHook(() => useRedemptions(filter));
    await waitFor(() => expect(result.current.status).toBe("ready"));

    filter = { status: "Completed" };
    rerender();
    await waitFor(() => expect(callCount).toBeGreaterThan(1));
  });

  it("returns paginated data", async () => {
    server.use(
      http.get("http://localhost/api/redemptions", () =>
        HttpResponse.json({ items: [], totalCount: 50, offset: 10, limit: 10 }),
      ),
    );
    const { result } = renderHook(() =>
      useRedemptions({ page: 2, pageSize: 10 }),
    );
    await waitFor(() => expect(result.current.status).toBe("ready"));
    expect(result.current.data!.totalCount).toBe(50);
    expect(result.current.data!.offset).toBe(10);
  });
});

/**
 * Sorting is server-side, so every column-header click is a request. Dropping back to a loading
 * state on each one would blank the table on every click.
 */
describe("useRedemptions — keeping the table populated", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  afterEach(() => {
    clearAccessToken();
  });

  const row = (id: string) => ({
    id,
    status: "Completed" as const,
    storeItemName: `Item ${id}`,
    storeName: "Store",
    attemptedAt: "2026-01-01T00:00:00Z",
    submittedAt: null,
    settledAt: null,
    failureCode: null,
    failureContext: null,
  });

  it("keeps the previously loaded rows visible while a new sort loads", async () => {
    server.use(
      http.get("http://localhost/api/redemptions", () =>
        HttpResponse.json({
          items: [row("1")],
          totalCount: 1,
          offset: 0,
          limit: 20,
        }),
      ),
    );

    const { result, rerender } = renderHook(
      ({ filter }: { filter: RedemptionFilter }) => useRedemptions(filter),
      { initialProps: { filter: { sort: "attemptedAt" } as RedemptionFilter } },
    );

    await waitFor(() => expect(result.current.data!.items).toHaveLength(1));

    let release: (() => void) | undefined;
    server.use(
      http.get("http://localhost/api/redemptions", async () => {
        await new Promise<void>((resolve) => {
          release = resolve;
        });
        return HttpResponse.json({
          items: [row("2")],
          totalCount: 1,
          offset: 0,
          limit: 20,
        });
      }),
    );

    rerender({ filter: { sort: "storeName" } as RedemptionFilter });

    await waitFor(() => expect(result.current.isRefreshing).toBe(true));
    // The table does not blank: the rows already fetched are still on screen.
    expect(result.current.status).toBe("ready");
    expect(result.current.data!.items).toHaveLength(1);

    release!();
    await waitFor(() => expect(result.current.data!.items[0].id).toBe("2"));
  });
});

/**
 * Redemption status advances server-side on a cron. Without a refresh of its own, a left-open
 * history showed whatever was true when it mounted — a submitted redemption stayed "Submitted" on
 * screen long after it had completed, which is the shape the stale-history bug took.
 */
describe("useRedemptions — staying current", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  afterEach(() => {
    clearAccessToken();
  });

  const rowWithStatus = (status: string) => ({
    id: "r1",
    status,
    storeItemName: "Item",
    storeName: "Store",
    attemptedAt: "2026-01-01T00:00:00Z",
    submittedAt: "2026-01-01T00:00:05Z",
    settledAt: null,
    failureCode: null,
    failureContext: null,
    pollFailures: 0,
    provider: "streamelements" as const,
  });

  const respondWith = (status: string) =>
    server.use(
      http.get("http://localhost/api/redemptions", () =>
        HttpResponse.json({
          items: [rowWithStatus(status)],
          totalCount: 1,
          offset: 0,
          limit: 20,
        }),
      ),
    );

  it("picks up a status that advanced server-side, without a filter change", async () => {
    respondWith("Submitted");

    const { result } = renderHook(() => useRedemptions({}, 20));

    await waitFor(() =>
      expect(result.current.data!.items[0].status).toBe("Submitted"),
    );

    respondWith("Completed");

    await waitFor(
      () => expect(result.current.data!.items[0].status).toBe("Completed"),
      { timeout: 3000 },
    );
  });

  it("does not refresh when the interval is switched off", async () => {
    let callCount = 0;
    server.use(
      http.get("http://localhost/api/redemptions", () => {
        callCount++;
        return HttpResponse.json({
          items: [],
          totalCount: 0,
          offset: 0,
          limit: 20,
        });
      }),
    );

    const { result } = renderHook(() => useRedemptions({}, 0));
    await waitFor(() => expect(result.current.status).toBe("ready"));

    await new Promise((resolve) => setTimeout(resolve, 100));

    expect(callCount).toBe(1);
  });

  /**
   * A background tick that fails must not replace a populated table with an error panel — that
   * loses more than the staleness it was trying to fix.
   */
  it("keeps the rows it has when a background refresh fails", async () => {
    respondWith("Submitted");

    const { result } = renderHook(() => useRedemptions({}, 20));

    await waitFor(() => expect(result.current.data!.items).toHaveLength(1));

    server.use(
      http.get(
        "http://localhost/api/redemptions",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );

    await new Promise((resolve) => setTimeout(resolve, 150));

    expect(result.current.status).toBe("ready");
    expect(result.current.data!.items).toHaveLength(1);
  });

  it("refreshes when a hidden tab becomes visible again", async () => {
    respondWith("Submitted");

    // Interval off, so only the visibility handler can drive the second read.
    const { result } = renderHook(() => useRedemptions({}, 50));
    await waitFor(() =>
      expect(result.current.data!.items[0].status).toBe("Submitted"),
    );

    respondWith("Completed");
    document.dispatchEvent(new Event("visibilitychange"));

    await waitFor(
      () => expect(result.current.data!.items[0].status).toBe("Completed"),
      { timeout: 3000 },
    );
  });
});
