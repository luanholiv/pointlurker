import { useCallback, useEffect, useRef, useState } from "react";
import { ApiError } from "../../lib/apiClient";
import { tApiError } from "../../i18n/errorCatalog";
import {
  activateStore,
  createStore,
  deactivateStore,
  fetchStoreDetail,
  fetchStores,
  fetchStoreSummary,
  lookupStore,
  removeStore,
  type CreateStoreRequest,
  type StoreDetail,
  type StoreListItem,
  type StoreLookupResponse,
  type StoreSummary,
} from "./storesApi";
import type { StoreProvider } from "./storeProviders";

export type StoreListFilter = {
  /** Matches a store's display name or its identifier. */
  search?: string;
  /** `true` for active only, `false` for paused only, omitted for both. */
  active?: boolean | null;
};

/**
 * Matches the item grid's debounce: long enough to swallow a burst of typing, short enough not to
 * read as lag.
 */
const SEARCH_DEBOUNCE_MS = 300;

const toQuery = (filter: StoreListFilter): URLSearchParams => {
  const params = new URLSearchParams();
  if (filter.search) params.set("q", filter.search);
  if (filter.active !== null && filter.active !== undefined) {
    params.set("active", filter.active ? "1" : "0");
  }
  return params;
};

/**
 * The watched-store list, narrowed by the server. The active and paused counts come from
 * `/api/stores/summary` rather than from this list: they are the same whatever the filter, so
 * deriving them from a narrowed list would make the headline numbers move as the user types.
 */
export const useStoreList = (filter: StoreListFilter = {}) => {
  const [stores, setStores] = useState<StoreListItem[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [summary, setSummary] = useState<StoreSummary>({
    activeCount: 0,
    pausedCount: 0,
  });
  const [status, setStatus] = useState<"loading" | "ready" | "error">(
    "loading",
  );

  const search = filter.search ?? "";
  const [debouncedSearch, setDebouncedSearch] = useState(search);

  const requestId = useRef(0);

  // Only the text term waits; the active/paused chips apply on the click that set them.
  const filterKey = toQuery({ ...filter, search: debouncedSearch }).toString();

  useEffect(() => {
    if (search === debouncedSearch) {
      return;
    }

    const timer = setTimeout(
      () => setDebouncedSearch(search),
      SEARCH_DEBOUNCE_MS,
    );
    return () => clearTimeout(timer);
  }, [search, debouncedSearch]);

  /**
   * The one request, issued by both the effect below and `reload`. Written as a promise chain
   * rather than `async`/`await` so every state update happens inside a `then`/`catch`, and guarded
   * by a monotonic id so a superseded response — a reload still in flight when the filter moves on
   * — cannot overwrite newer rows.
   */
  const loadList = useCallback(() => {
    const id = ++requestId.current;

    return fetchStores(new URLSearchParams(filterKey))
      .then((page) => {
        if (id !== requestId.current) {
          return;
        }
        setStores(page.items);
        setTotalCount(page.totalCount);
        setStatus("ready");
      })
      .catch(() => {
        if (id !== requestId.current) {
          return;
        }
        setStatus("error");
      });
  }, [filterKey]);

  const loadSummary = useCallback(
    () =>
      fetchStoreSummary()
        .then(setSummary)
        .catch(() => {
          // The list is still usable without its headline counts.
        }),
    [],
  );

  useEffect(() => {
    void loadList();
  }, [loadList]);

  // Deliberately not keyed on the filter: these totals span every store the caller owns, so they
  // move when the stores do, not when the filter does. `reload` is what signals that.
  useEffect(() => {
    void loadSummary();
  }, [loadSummary]);

  /**
   * Refetches both halves. The counts are not derived from the rows, so a caller that mutated
   * a store — paused, removed or created one — has to refresh them explicitly or the headline keeps
   * describing the state before the mutation.
   */
  const reload = useCallback(
    () => Promise.all([loadList(), loadSummary()]).then(() => undefined),
    [loadList, loadSummary],
  );

  return { stores, totalCount, summary, status, reload };
};

export const useStoreDetail = (id: string) => {
  const [store, setStore] = useState<StoreDetail | null>(null);
  const [status, setStatus] = useState<
    "loading" | "ready" | "error" | "not_found"
  >("loading");

  useEffect(() => {
    let active = true;

    fetchStoreDetail(id)
      .then((data) => {
        if (active) {
          setStore(data);
          setStatus("ready");
        }
      })
      .catch((err: unknown) => {
        if (active) {
          // A missing store arrives as problem+json, so the status is on the contract. Reading it
          // off the message text only ever matched the bodyless shape the endpoint never returns.
          const notFound = err instanceof ApiError && err.status === 404;
          setStatus(notFound ? "not_found" : "error");
        }
      });

    return () => {
      active = false;
    };
  }, [id]);

  return { store, status };
};

export const useCreateStore = () => {
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");
  // The rejection itself, kept alongside its message: per-field failures are read from it by the
  // dialog, which owns the map from wire field names to the labels a user recognises.
  const [error, setError] = useState<unknown>(null);

  const execute = useCallback(async (payload: CreateStoreRequest) => {
    setStatus("saving");
    setErrorMessage("");
    setError(null);
    try {
      const result = await createStore(payload);
      setStatus("idle");
      return result;
    } catch (err) {
      setStatus("error");
      setErrorMessage(tApiError(err, "stores.createFailed"));
      setError(err);
      return null;
    }
  }, []);

  const reset = useCallback(() => {
    setStatus("idle");
    setErrorMessage("");
    setError(null);
  }, []);

  return { execute, status, errorMessage, error, reset };
};

export const useActivateStore = () => {
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");

  const execute = useCallback(async (id: string) => {
    setStatus("saving");
    try {
      await activateStore(id);
      setStatus("idle");
      return true;
    } catch {
      setStatus("error");
      return false;
    }
  }, []);

  return { execute, status };
};

export const useDeactivateStore = () => {
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");

  const execute = useCallback(async (id: string) => {
    setStatus("saving");
    try {
      await deactivateStore(id);
      setStatus("idle");
      return true;
    } catch {
      setStatus("error");
      return false;
    }
  }, []);

  return { execute, status };
};

export const useRemoveStore = () => {
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");

  const execute = useCallback(async (id: string) => {
    setStatus("saving");
    try {
      await removeStore(id);
      setStatus("idle");
      return true;
    } catch {
      setStatus("error");
      return false;
    }
  }, []);

  return { execute, status };
};

type LookupState =
  | { status: "idle" }
  | { status: "loading"; provider: StoreProvider }
  | {
      status: "resolved";
      provider: StoreProvider;
      externalId: string;
      displayName: string;
      itemCount: number | null;
    }
  | { status: "error"; provider: StoreProvider; message: string };

export const useStoreLookup = () => {
  const [state, setState] = useState<LookupState>({ status: "idle" });

  const lookup = useCallback(
    async (provider: StoreProvider, query?: string) => {
      setState({ status: "loading", provider });
      try {
        const result: StoreLookupResponse = await lookupStore(provider, query);
        setState({
          status: "resolved",
          provider,
          externalId: result.externalId,
          displayName: result.displayName,
          itemCount: result.itemCount,
        });
      } catch (err) {
        setState({
          status: "error",
          provider,
          message: tApiError(err, "errors.provider_api.unreachable"),
        });
      }
    },
    [],
  );

  const reset = useCallback(() => setState({ status: "idle" }), []);

  return { state, lookup, reset };
};
