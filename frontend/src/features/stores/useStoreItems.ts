import { useCallback, useEffect, useRef, useState } from "react";
import { fetchStoreItems, type StoreItemDetail } from "./storesApi";
import { serializeFilters } from "./useStoreFilters";
import type { StoreItemFilters } from "./types";

/** Long enough to swallow a burst of typing, short enough not to read as lag. */
const SEARCH_DEBOUNCE_MS = 300;

/**
 * Chosen against a full store rather than a screenful: no store observed holds more than ~500
 * items, so 48 makes a full scroll ~11 round trips instead of ~21, and shortens the window in
 * which a concurrent sync can shift the result set under the reader.
 */
const PAGE_SIZE = 48;

type Status = "loading" | "ready" | "error";

type Loaded = {
  /** The query these rows answer. Comparing it to the current one is what "stale" means here. */
  key: string;
  items: StoreItemDetail[];
  totalCount: number;
  exhausted: boolean;
};

const EMPTY: Loaded = { key: "", items: [], totalCount: 0, exhausted: false };

/**
 * Reads one store's items from the server. Every filtering and sorting rule lives in the backend;
 * this hook only turns filter state into a query string, accumulates slices, and keeps the grid
 * usable while a new query is in flight.
 *
 * Three properties are worth naming because each fixes a specific failure:
 *
 * - **Previous items stay visible.** Dropping back to `loading` on every filter change blanks the
 *   grid, which on a search box means blanking it on every keystroke. `isRefreshing` is *derived*
 *   — the loaded rows answer an older query than the current one — so no effect has to set it.
 * - **A late response cannot overwrite a newer one.** Aborting is not enough on its own — a request
 *   can resolve before the abort lands — so a monotonic request id decides what may be applied.
 * - **Appends deduplicate, and loading stops when a slice adds nothing.** Store sync can change
 *   which items match mid-scroll. Terminating only on `items.length >= totalCount` would leave a
 *   "Load more" that returns nothing forever once a row has been skipped.
 */
export const useStoreItems = (storeId: string, filters: StoreItemFilters) => {
  const [loaded, setLoaded] = useState<Loaded>(EMPTY);
  const [status, setStatus] = useState<Status>("loading");
  const [debouncedSearch, setDebouncedSearch] = useState(filters.search);

  const requestId = useRef(0);
  const inFlight = useRef<AbortController | null>(null);
  const seenIds = useRef(new Set<string>());

  // The discrete filters, serialized. A string rather than the filter object because the object is
  // rebuilt on every render, which would make every dependent callback — and so the effect that
  // fetches — fire again on every render.
  const filterKey = serializeFilters({ ...filters, search: "" }).toString();

  const query = useCallback(
    (offset: number, search: string) => {
      const params = new URLSearchParams(filterKey);
      // `serializeFilters` omits an empty search, so this reproduces exactly what it would emit.
      if (search) {
        params.set("q", search);
      }
      params.set("offset", String(offset));
      params.set("limit", String(PAGE_SIZE));
      return params;
    },
    [filterKey],
  );

  // Identifies the query the grid should currently be showing. Built from the same parameters the
  // request uses, so the two cannot describe different things.
  const key = `${storeId}?${filterKey}&q=${debouncedSearch}`;

  useEffect(() => {
    if (filters.search === debouncedSearch) {
      return;
    }

    const timer = setTimeout(
      () => setDebouncedSearch(filters.search),
      SEARCH_DEBOUNCE_MS,
    );
    return () => clearTimeout(timer);
  }, [filters.search, debouncedSearch]);

  /**
   * Written as a promise chain rather than `async`/`await` on purpose: every state update happens
   * inside a `then`/`catch` callback, so nothing is set synchronously when an effect calls this.
   */
  const request = useCallback(
    (offset: number, mode: "reset" | "append", forKey: string) => {
      inFlight.current?.abort();
      const controller = new AbortController();
      inFlight.current = controller;
      const id = ++requestId.current;

      return fetchStoreItems(
        storeId,
        query(offset, debouncedSearch),
        controller.signal,
      )
        .then((page) => {
          // Only the newest request may write. Abort alone cannot guarantee this: a request can
          // resolve in the moment between being superseded and the abort taking effect.
          if (id !== requestId.current) {
            return;
          }

          if (mode === "reset") {
            seenIds.current = new Set(page.items.map((item) => item.id));
            setLoaded({
              key: forKey,
              items: page.items,
              totalCount: page.totalCount,
              exhausted: page.items.length === 0,
            });
          } else {
            const fresh = page.items.filter(
              (item) => !seenIds.current.has(item.id),
            );
            fresh.forEach((item) => seenIds.current.add(item.id));
            setLoaded((current) => ({
              key: forKey,
              items: [...current.items, ...fresh],
              totalCount: page.totalCount,
              // No new ids means there is nothing further to reach, whatever the total claims.
              exhausted: fresh.length === 0,
            }));
          }

          setStatus("ready");
        })
        .catch(() => {
          if (controller.signal.aborted || id !== requestId.current) {
            return;
          }
          setStatus("error");
        });
    },
    [storeId, query, debouncedSearch],
  );

  useEffect(() => {
    void request(0, "reset", key);

    return () => inFlight.current?.abort();
  }, [request, key]);

  // The rows on screen answer a query that is no longer the current one: a new one is on its way,
  // and the grid keeps showing these until it lands.
  const isRefreshing = status === "ready" && loaded.key !== key;
  const hasMore = !loaded.exhausted && loaded.items.length < loaded.totalCount;

  const loadMore = useCallback(() => {
    if (!hasMore || isRefreshing) {
      return;
    }

    void request(loaded.items.length, "append", key);
  }, [hasMore, isRefreshing, loaded.items.length, request, key]);

  const reload = useCallback(() => {
    void request(0, "reset", key);
  }, [request, key]);

  return {
    items: loaded.items,
    totalCount: loaded.totalCount,
    status,
    isRefreshing,
    hasMore,
    loadMore,
    reload,
  };
};
