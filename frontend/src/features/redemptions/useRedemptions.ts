import { useCallback, useEffect, useRef, useState } from "react";
import {
  fetchRedemptions,
  type RedemptionFilter,
  type RedemptionListResponse,
} from "./redemptionsApi";

/**
 * How often a mounted history re-reads itself. Redemption status advances server-side on a cron,
 * so without this the rows on screen are only ever as fresh as the last filter change or page
 * load — a submitted redemption would sit there looking submitted long after it had completed.
 */
const REFRESH_INTERVAL_MS = 30_000;

/**
 * The redemption history, ordered and paged by the server.
 *
 * Loaded rows stay visible while the next request is in flight. Sorting is server-side, so every
 * column-header click is a request, and dropping back to a loading state would blank the table on
 * each one.
 */
export const useRedemptions = (
  filter: RedemptionFilter,
  refreshIntervalMs: number = REFRESH_INTERVAL_MS,
) => {
  const [loaded, setLoaded] = useState<{
    key: string;
    data: RedemptionListResponse;
  } | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">(
    "loading",
  );

  // Serialized filter used as a stable dependency key. Extracted to a variable
  // so the hooks linter can statically check the dependency arrays below.
  const filterKey = JSON.stringify(filter);
  const requestId = useRef(0);

  // Set on success and read in the failure path, so neither makes `run` a new function: a
  // background refresh rebuilt on every arrival would reset its own interval each tick. Never
  // cleared, because loaded rows are deliberately kept on screen once fetched.
  const hasRows = useRef(false);

  /**
   * A promise chain rather than `async`/`await`: every state update lands in a `then`/`catch`
   * callback, so nothing is set synchronously when the effect below calls this.
   */
  const run = useCallback(() => {
    const id = ++requestId.current;

    return fetchRedemptions(JSON.parse(filterKey) as RedemptionFilter)
      .then((result) => {
        // Only the newest request may write: a slower earlier one must not overwrite it.
        if (id !== requestId.current) return;
        hasRows.current = true;
        setLoaded({ key: filterKey, data: result });
        setStatus("ready");
      })
      .catch(() => {
        if (id !== requestId.current) return;
        // A background refresh that fails leaves the rows it already has. Replacing a populated
        // table with an error panel over one bad tick loses more than the staleness costs.
        if (hasRows.current) return;
        setStatus("error");
      });
  }, [filterKey]);

  useEffect(() => {
    void run();
  }, [run]);

  // Keeps a left-open history current, and catches up a tab that was in the background while the
  // server moved on. Both are silent: `run` writes rows without dropping back to a loading state.
  useEffect(() => {
    if (refreshIntervalMs <= 0) return;

    const timer = setInterval(() => {
      if (document.visibilityState === "hidden") return;
      void run();
    }, refreshIntervalMs);

    const onVisible = () => {
      if (document.visibilityState === "visible") void run();
    };
    document.addEventListener("visibilitychange", onVisible);

    return () => {
      clearInterval(timer);
      document.removeEventListener("visibilitychange", onVisible);
    };
  }, [run, refreshIntervalMs]);

  // Derived, not stored: the rows on screen answer an older query than the one now in flight.
  const isRefreshing = status === "ready" && loaded?.key !== filterKey;

  return { data: loaded?.data ?? null, status, isRefreshing, reload: run };
};
