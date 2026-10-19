import { useEffect, useState } from "react";
import { fetchStoreItemFacets } from "./storesApi";

export type StoreFilterOptions = {
  categories: string[];
  costBounds: { min: number; max: number };
};

const EMPTY: StoreFilterOptions = {
  categories: [],
  costBounds: { min: 0, max: 0 },
};

/**
 * The values the filter controls offer. Derived by the server across the whole store, not from the
 * loaded page: computing them from the current result would make the category dropdown shrink as
 * the user picks from it.
 *
 * Fetched once per store visit rather than with every slice — they cannot change as a filter
 * changes, which is exactly why they are not part of the item page response.
 */
export const useStoreItemFacets = (storeId: string): StoreFilterOptions => {
  const [options, setOptions] = useState<StoreFilterOptions>(EMPTY);

  useEffect(() => {
    const controller = new AbortController();

    fetchStoreItemFacets(storeId, controller.signal)
      .then((facets) =>
        setOptions({
          categories: facets.categories,
          costBounds: { min: facets.minCost, max: facets.maxCost },
        }),
      )
      .catch(() => {
        // A failed facet fetch leaves the controls without their option lists; the grid itself
        // still works, so this is not worth failing the page over.
      });

    return () => controller.abort();
  }, [storeId]);

  return options;
};
