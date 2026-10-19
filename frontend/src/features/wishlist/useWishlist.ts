import { useCallback, useEffect, useRef, useState } from "react";
import { tApiError } from "../../i18n/errorCatalog";
import {
  addWishlistEntry,
  fetchWishlistEntry,
  fetchWishlistGrouped,
  removeWishlistEntry,
  updateWishlistEntry,
  type CreateWishlistEntryRequest,
  type UpdateWishlistEntryRequest,
  type WishlistEntryDetail,
  type WishlistFilter,
  type WishlistGroupedList,
} from "./wishlistApi";

/** Matches the other lists' debounce: long enough to swallow a burst of typing, short enough not
 * to read as lag. */
const SEARCH_DEBOUNCE_MS = 300;

export const useWishlistList = (filter: WishlistFilter = {}) => {
  const [data, setData] = useState<WishlistGroupedList | null>(null);
  const [status, setStatus] = useState<"loading" | "ready" | "error">(
    "loading",
  );

  const search = filter.search ?? "";
  const [debouncedSearch, setDebouncedSearch] = useState(search);

  const requestId = useRef(0);

  // The filter object is rebuilt on every render, so its serialization — not its identity — is
  // what tells one query from another. Reading the filter back out of the key is what lets the
  // fetch depend on the value rather than the object, with no exhaustive-deps suppression.
  // Only the text term waits; every other filter applies on the interaction that set it.
  const filterKey = JSON.stringify({ ...filter, search: debouncedSearch });

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
   * The one request, issued by both the effect below and `reload`.
   *
   * It deliberately does not drop back to `loading`: the page renders its list only while
   * `status === "ready"`, so doing that would replace the whole list with a loading line after
   * every add, edit and remove. The monotonic id is what keeps that safe — a reload begun under one
   * filter cannot land after, and overwrite, the results of a newer one.
   */
  const load = useCallback(() => {
    const id = ++requestId.current;

    return fetchWishlistGrouped(JSON.parse(filterKey) as WishlistFilter)
      .then((result) => {
        if (id !== requestId.current) {
          return;
        }
        setData(result);
        setStatus("ready");
      })
      .catch(() => {
        if (id !== requestId.current) {
          return;
        }
        setStatus("error");
      });
  }, [filterKey]);

  useEffect(() => {
    void load();
  }, [load]);

  return { data, status, reload: load };
};

export const useAddWishlist = () => {
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");
  // The rejection itself, kept alongside its message: per-field failures are read from it by the
  // modal, which owns the map from wire field names to the labels its inputs carry.
  const [error, setError] = useState<unknown>(null);

  const execute = useCallback(async (payload: CreateWishlistEntryRequest) => {
    setStatus("saving");
    setErrorMessage("");
    setError(null);
    try {
      const result = await addWishlistEntry(payload);
      setStatus("idle");
      return result;
    } catch (err) {
      setStatus("error");
      setErrorMessage(tApiError(err, "wishlist.addFailed"));
      setError(err);
      return null;
    }
  }, []);

  return { execute, status, errorMessage, error };
};

export const useUpdateWishlist = () => {
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");
  const [error, setError] = useState<unknown>(null);

  const execute = useCallback(
    async (id: string, payload: UpdateWishlistEntryRequest) => {
      setStatus("saving");
      setErrorMessage("");
      setError(null);
      try {
        await updateWishlistEntry(id, payload);
        setStatus("idle");
        return true;
      } catch (err) {
        setStatus("error");
        setErrorMessage(tApiError(err, "wishlist.updateFailed"));
        setError(err);
        return false;
      }
    },
    [],
  );

  return { execute, status, errorMessage, error };
};

/**
 * The failure branch carries its own message rather than leaving the caller to read it back off
 * the hook.
 *
 * A handler that `await`s `execute` is still running in the closure from the render that started
 * the call, so an `errorMessage` piece of state would not have been re-read yet — it is reliably
 * the *previous* value there, i.e. empty on a first failure. That produced a toast with a blank
 * body. The two hooks below are consumed imperatively, from a click handler, so the message has
 * to travel with the result. The `add`/`update` hooks keep `errorMessage` state because the modal
 * consumes theirs while rendering, where reading state is correct.
 */
export type WishlistActionFailure = { ok: false; message: string };
export type RemoveWishlistResult = { ok: true } | WishlistActionFailure;
export type WishlistEntryDetailResult =
  { ok: true; entry: WishlistEntryDetail } | WishlistActionFailure;

export const useRemoveWishlist = () => {
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");

  const execute = useCallback(
    async (id: string): Promise<RemoveWishlistResult> => {
      setStatus("saving");
      try {
        await removeWishlistEntry(id);
        setStatus("idle");
        return { ok: true };
      } catch (err) {
        setStatus("error");
        return {
          ok: false,
          message: tApiError(err, "wishlist.toast.removeError"),
        };
      }
    },
    [],
  );

  return { execute, status };
};

/**
 * Loads one entry's full detail, which is what the edit modal needs before it can open.
 *
 * The fetch sits behind a hook rather than being called straight from a handler so that the
 * `ApiError` → catalog mapping lives here once, instead of once per call site — the store item
 * card and the wishlist card both need it.
 */
export const useWishlistEntryDetail = () => {
  const [status, setStatus] = useState<"idle" | "loading" | "error">("idle");

  const execute = useCallback(
    async (id: string): Promise<WishlistEntryDetailResult> => {
      setStatus("loading");
      try {
        const entry = await fetchWishlistEntry(id);
        setStatus("idle");
        return { ok: true, entry };
      } catch (err) {
        setStatus("error");
        return {
          ok: false,
          message: tApiError(err, "wishlist.toast.loadEntryError"),
        };
      }
    },
    [],
  );

  return { execute, status };
};
