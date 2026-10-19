import { getAppConfig } from "../../config";
import { createApiClient } from "../../lib/apiClient";
import type { StoreProvider } from "./storeProviders";

export type StoreListItem = {
  id: string;
  channelId: string;
  displayName: string;
  isActive: boolean;
  lastSyncedAt: string | null;
  itemCount: number;
  avatarUrl?: string | null;
  provider: StoreProvider;
};

/**
 * One card in a store's item grid, as the server sends it. It carries everything a card renders or
 * acts on, so rendering never costs a second request — and nothing more: the fields the grid never
 * showed (type, public, input definitions, preview image) are not on the wire.
 */
export type StoreItemDetail = {
  id: string;
  providerItemId: string;
  name: string;
  description: string | null;
  cost: number;
  categoryName: string;
  subscriberOnly: boolean;
  isAvailable: boolean;
  quantityCurrent: number;
  quantityTotal: number;
  allowMessages: boolean;
  thumbnailUrl: string | null;
  wishlistEntryId: string | null;
  providerCreatedAt: string | null;
};

/**
 * A store's envelope. Items are fetched separately — filtered, sorted and paged by the server —
 * so `itemCount` is what distinguishes "this store has no items" from "no items match the current
 * filters"; a filtered page total cannot answer the first question.
 */
export type StoreDetail = {
  id: string;
  channelId: string;
  displayName: string;
  isActive: boolean;
  lastSyncedAt: string | null;
  createdAt: string;
  itemCount: number;
  missingLabels: string[];
  avatarUrl?: string | null;
  provider: StoreProvider;
};

/** The envelope every searchable list endpoint answers with. */
export type Page<TRow> = {
  items: TRow[];
  totalCount: number;
  offset: number;
  limit: number;
};

/**
 * The values a store's filter controls offer, computed across all of its items and unaffected by
 * the active filter — fetched once per store visit rather than with every slice.
 */
export type StoreItemFacets = {
  categories: string[];
  minCost: number;
  maxCost: number;
};

/** How many of the caller's stores are active and paused, across all of them. */
export type StoreSummary = {
  activeCount: number;
  pausedCount: number;
};

/**
 * How the backend resolver matched a raw label to a stored value. Sent as a name, not an
 * ordinal — the UI switches on it to label each `fed by` chip.
 */
export type InputResolutionSource = "Exact" | "Alias" | "Fuzzy";

export type StoreProfileRequester = {
  rawLabel: string;
  source: InputResolutionSource;
  confidence: number;
  itemCount: number;
};

export type StoreProfileUnresolvedInput = {
  rawLabel: string;
  bestCanonical: string | null;
  bestScore: number;
  /** Set when a manual alias points at a canonical label that holds no value yet. */
  aliasTarget: string | null;
  itemCount: number;
  blockedEntryCount: number;
};

export type StoreProfileResolvedInput = {
  labelCanonical: string;
  updatedAt: string;
  /** Empty ⇒ no item currently asks for this slot. */
  requestedBy: StoreProfileRequester[];
};

/**
 * Both arrays arrive pre-sorted. The UI renders array order verbatim — every threshold,
 * comparator and normalization rule lives in the backend resolver.
 */
export type StoreInputProfile = {
  unresolved: StoreProfileUnresolvedInput[];
  resolved: StoreProfileResolvedInput[];
  canonicalLabels: string[];
};

export type StoreProfileValueUpsert = {
  labelCanonical: string;
  value: string | null;
};

/**
 * A StreamElements store names its channel. A provider with one fixed store sends only its
 * provider: the server resolves that store's identity itself and refuses one sent to it.
 */
export type CreateStoreRequest =
  | { provider: "streamelements"; channelId: string; displayName: string }
  | { provider: "gaupoints" };

/** `itemCount` is null when the lookup did not read the catalogue, which is not an empty store. */
export type StoreLookupResponse = {
  externalId: string;
  handle: string;
  displayName: string;
  avatarUrl: string | null;
  itemCount: number | null;
};

// The auth-aware client injects the bearer token from storage and handles 401 refresh.
const getApi = () => createApiClient({ baseUrl: getAppConfig().apiBaseUrl });

export const fetchStores = (query?: URLSearchParams) => {
  const qs = query?.toString();
  return getApi().get<Page<StoreListItem>>(
    qs ? `/api/stores?${qs}` : "/api/stores",
  );
};

export const fetchStoreSummary = () =>
  getApi().get<StoreSummary>("/api/stores/summary");

/**
 * One slice of a store's items. `query` is the browser URL's own filter parameters — the endpoint
 * answers to the same names — plus the slice bounds. `signal` aborts a superseded request.
 */
export const fetchStoreItems = (
  id: string,
  query: URLSearchParams,
  signal?: AbortSignal,
) =>
  getApi().get<Page<StoreItemDetail>>(
    `/api/stores/${id}/items?${query.toString()}`,
    { signal },
  );

export const fetchStoreItemFacets = (id: string, signal?: AbortSignal) =>
  getApi().get<StoreItemFacets>(`/api/stores/${id}/items/facets`, { signal });

export const fetchStoreDetail = (id: string) =>
  getApi().get<StoreDetail>(`/api/stores/${id}`);

export const createStore = (payload: CreateStoreRequest) =>
  getApi().post<StoreDetail>("/api/stores", payload);

export const activateStore = (id: string) =>
  getApi().patch<void>(`/api/stores/${id}/activate`);

export const deactivateStore = (id: string) =>
  getApi().patch<void>(`/api/stores/${id}/deactivate`);

export const removeStore = (id: string) => getApi().del(`/api/stores/${id}`);

/** `query` is what the owner typed; a provider with one fixed store takes none. */
export const lookupStore = (provider: StoreProvider, query?: string) => {
  const params = new URLSearchParams({ provider });
  if (query !== undefined) params.set("query", query);
  return getApi().get<StoreLookupResponse>(
    `/api/stores/lookup?${params.toString()}`,
  );
};

export const fetchStoreProfile = (id: string) =>
  getApi().get<StoreInputProfile>(`/api/stores/${id}/profile`);

export const upsertStoreProfileValues = (
  id: string,
  values: StoreProfileValueUpsert[],
) => getApi().put<void>(`/api/stores/${id}/profile/values`, { values });

export const upsertStoreProfileAlias = (
  id: string,
  rawLabel: string,
  labelCanonical: string,
) =>
  getApi().post<void>(`/api/stores/${id}/profile/aliases`, {
    rawLabel,
    labelCanonical,
  });

// Labels are arbitrary streamer-authored strings, so they travel in the query string with a
// single encoding rule rather than as a path segment. Both deletes are idempotent (204).
export const deleteStoreProfileValue = (id: string, label: string) =>
  getApi().del(
    `/api/stores/${id}/profile/values?label=${encodeURIComponent(label)}`,
  );

export const deleteStoreProfileAlias = (id: string, rawLabel: string) =>
  getApi().del(
    `/api/stores/${id}/profile/aliases?rawLabel=${encodeURIComponent(rawLabel)}`,
  );
