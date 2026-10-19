import { useCallback, useMemo } from "react";
import { useSearchParams } from "react-router";
import type { StoreItemFilters } from "./types";
import { DEFAULT_FILTERS } from "./types";

const VALID_SORT_FIELDS = new Set<StoreItemFilters["sort"]>([
  "relevance",
  "name",
  "cost",
  "stock",
  "date",
]);
const VALID_SORT_DIRS = new Set<StoreItemFilters["sortDir"]>(["asc", "desc"]);

/**
 * What an unstated ordering means, which depends on whether there is a term to rank by. The server
 * makes the same choice from the same input, and it has to: this is also the value `serializeFilters`
 * omits, so if the two disagreed the URL would leave out the very parameter that was needed and the
 * dropdown would name an ordering the grid is not in.
 */
export function defaultSortFor(search: string): StoreItemFilters["sort"] {
  return search ? "relevance" : "name";
}

function parseFilters(params: URLSearchParams): StoreItemFilters {
  const search = params.get("q") ?? DEFAULT_FILTERS.search;

  const availRaw = params.get("avail");
  const available = availRaw === "0" ? false : DEFAULT_FILTERS.available;

  const subRaw = params.get("sub");
  const subscriberOnly = subRaw === "1" ? true : DEFAULT_FILTERS.subscriberOnly;

  const featRaw = params.get("feat");
  const featured = featRaw === "1" ? true : DEFAULT_FILTERS.featured;

  const catRaw = params.get("cat");
  const categories = catRaw
    ? catRaw.split(",").filter(Boolean)
    : DEFAULT_FILTERS.categories;

  const minRaw = params.get("min");
  const costMin =
    minRaw !== null && !isNaN(Number(minRaw))
      ? Number(minRaw)
      : DEFAULT_FILTERS.costMin;

  const maxRaw = params.get("max");
  const costMax =
    maxRaw !== null && !isNaN(Number(maxRaw))
      ? Number(maxRaw)
      : DEFAULT_FILTERS.costMax;

  const sortRaw = params.get("sort") as StoreItemFilters["sort"] | null;
  const sort =
    sortRaw && VALID_SORT_FIELDS.has(sortRaw)
      ? sortRaw
      : defaultSortFor(search);

  const dirRaw = params.get("dir") as StoreItemFilters["sortDir"] | null;
  const sortDir =
    dirRaw && VALID_SORT_DIRS.has(dirRaw) ? dirRaw : DEFAULT_FILTERS.sortDir;

  return {
    search,
    available,
    subscriberOnly,
    featured,
    categories,
    costMin,
    costMax,
    sort,
    sortDir,
  };
}

/**
 * Turns the filter state into query-string parameters. The API answers to exactly these names, so
 * this one serializer produces both the browser URL and the API query string — no translation layer
 * between them, and a bookmarked filtered view resolves identically on both sides.
 */
export function serializeFilters(filters: StoreItemFilters): URLSearchParams {
  const params = new URLSearchParams();

  if (filters.search) params.set("q", filters.search);
  if (!filters.available) params.set("avail", "0");
  if (filters.subscriberOnly) params.set("sub", "1");
  if (filters.featured) params.set("feat", "1");
  if (filters.categories.length > 0)
    params.set("cat", filters.categories.join(","));
  if (filters.costMin !== null) params.set("min", String(filters.costMin));
  if (filters.costMax !== null) params.set("max", String(filters.costMax));
  if (filters.sort !== defaultSortFor(filters.search))
    params.set("sort", filters.sort);
  if (filters.sortDir !== DEFAULT_FILTERS.sortDir)
    params.set("dir", filters.sortDir);

  return params;
}

export function useStoreFilters() {
  const [searchParams, setSearchParams] = useSearchParams();

  const filters = useMemo(() => parseFilters(searchParams), [searchParams]);

  const update = useCallback(
    (patch: Partial<StoreItemFilters>) => {
      setSearchParams(
        (prev) => {
          const next = { ...parseFilters(prev), ...patch };

          // Losing the term loses the ranking with it, whichever setter dropped it: there is
          // nothing left to order by, so the server would answer by name while the dropdown still
          // read "Relevance". Enforced here rather than in `setSearch` so clearing the term any
          // other way cannot leave the pair inconsistent.
          return serializeFilters(
            next.search === "" && next.sort === "relevance"
              ? { ...next, sort: defaultSortFor("") }
              : next,
          );
        },
        { replace: true },
      );
    },
    [setSearchParams],
  );

  const setSearch = useCallback(
    (value: string) => update({ search: value }),
    [update],
  );
  const setAvailable = useCallback(
    (value: boolean) => update({ available: value }),
    [update],
  );
  const setSubscriberOnly = useCallback(
    (value: boolean) => update({ subscriberOnly: value }),
    [update],
  );
  const setFeatured = useCallback(
    (value: boolean) => update({ featured: value }),
    [update],
  );
  const setCategories = useCallback(
    (value: string[]) => update({ categories: value }),
    [update],
  );
  const setCostMin = useCallback(
    (value: number | null) => update({ costMin: value }),
    [update],
  );
  const setCostMax = useCallback(
    (value: number | null) => update({ costMax: value }),
    [update],
  );
  const setSort = useCallback(
    (field: StoreItemFilters["sort"]) => update({ sort: field }),
    [update],
  );
  const setSortDir = useCallback(
    (dir: StoreItemFilters["sortDir"]) => update({ sortDir: dir }),
    [update],
  );

  const resetAll = useCallback(() => {
    setSearchParams(serializeFilters(DEFAULT_FILTERS), { replace: true });
  }, [setSearchParams]);

  const hasActiveFilters = useMemo(() => {
    return (
      filters.search !== DEFAULT_FILTERS.search ||
      filters.available !== DEFAULT_FILTERS.available ||
      filters.subscriberOnly !== DEFAULT_FILTERS.subscriberOnly ||
      filters.featured !== DEFAULT_FILTERS.featured ||
      filters.categories.length > 0 ||
      filters.costMin !== null ||
      filters.costMax !== null ||
      filters.sort !== DEFAULT_FILTERS.sort ||
      filters.sortDir !== DEFAULT_FILTERS.sortDir
    );
  }, [filters]);

  return {
    filters,
    setSearch,
    setAvailable,
    setSubscriberOnly,
    setFeatured,
    setCategories,
    setCostMin,
    setCostMax,
    setSort,
    setSortDir,
    resetAll,
    hasActiveFilters,
  };
}
