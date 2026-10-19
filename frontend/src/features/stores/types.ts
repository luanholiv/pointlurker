export type StoreItemFilters = {
  search: string;
  available: boolean;
  subscriberOnly: boolean;
  featured: boolean;
  categories: string[];
  costMin: number | null;
  costMax: number | null;
  sort: "relevance" | "name" | "cost" | "stock" | "date";
  sortDir: "asc" | "desc";
};

export const DEFAULT_FILTERS: StoreItemFilters = {
  search: "",
  available: true,
  subscriberOnly: false,
  featured: false,
  categories: [],
  costMin: null,
  costMax: null,
  sort: "name",
  sortDir: "asc",
};
