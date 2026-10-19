import { getAppConfig } from "../../config";
import { createApiClient } from "../../lib/apiClient";

export type WishlistEntryListItem = {
  id: string;
  storeItemId: string;
  itemName: string;
  itemCost: number;
  isAvailable: boolean;
  notifyOnAvailable: boolean;
  autoRedeem: boolean;
  /** `null` means unlimited. */
  desiredRedemptions: number | null;
  quotaUsed: number;
  isBlocked: boolean;
  missingLabels: string[];
  createdAt: string;
  thumbnailUrl?: string | null;
};

export type WishlistStoreGroup = {
  storeId: string;
  storeName: string;
  isActive: boolean;
  lastSyncedAt: string | null;
  entries: WishlistEntryListItem[];
  avatarUrl?: string | null;
};

/**
 * The shared page envelope, where one row is a store group. The group shape is unchanged — the
 * envelope constrains the envelope, not the row — so a store's fields still arrive once rather
 * than repeated on each of its entries.
 */
export type WishlistGroupedList = {
  items: WishlistStoreGroup[];
  totalCount: number;
  offset: number;
  limit: number;
};

export type WishlistFilter = {
  /** Item-name substring. */
  search?: string;
  /** Restrict to entries whose item is available. */
  availableOnly?: boolean;
  storeId?: string;
};

export type WishlistEntryDetail = {
  id: string;
  storeItemId: string;
  itemName: string;
  itemCost: number;
  isAvailable: boolean;
  storeName: string;
  storeId: string;
  notifyOnAvailable: boolean;
  autoRedeem: boolean;
  /** `null` means unlimited. */
  desiredRedemptions: number | null;
  quotaUsed: number;
  allowMessages: boolean;
  redemptionMessage: string | null;
  isBlocked: boolean;
  missingLabels: string[];
  createdAt: string;
  updatedAt: string;
};

export type CreateWishlistEntryRequest = {
  storeItemId: string;
  notifyOnAvailable: boolean;
  autoRedeem: boolean;
  redemptionMessage?: string | null;
  /**
   * Omit and the backend defaults to 1; send `null` to ask for unlimited. Both requests below
   * always send it, so the default is a guard for other callers rather than something we rely on.
   */
  desiredRedemptions?: number | null;
};

export type UpdateWishlistEntryRequest = {
  notifyOnAvailable: boolean;
  autoRedeem: boolean;
  redemptionMessage?: string | null;
  desiredRedemptions?: number | null;
};

export type WishlistEntryCreated = {
  id: string;
  storeItemId: string;
  itemName: string;
  storeName: string;
  notifyOnAvailable: boolean;
  autoRedeem: boolean;
  createdAt: string;
};

// The auth-aware client injects the bearer token from storage and handles 401 refresh.
const getApi = () => createApiClient({ baseUrl: getAppConfig().apiBaseUrl });

export const fetchWishlistGrouped = (filter: WishlistFilter = {}) => {
  const params = new URLSearchParams();
  if (filter.search) params.set("q", filter.search);
  if (filter.availableOnly) params.set("avail", "1");
  if (filter.storeId) params.set("store", filter.storeId);

  const qs = params.toString();
  return getApi().get<WishlistGroupedList>(
    qs ? `/api/wishlist?${qs}` : "/api/wishlist",
  );
};

export const fetchWishlistEntry = (id: string) =>
  getApi().get<WishlistEntryDetail>(`/api/wishlist/${id}`);

export const addWishlistEntry = (payload: CreateWishlistEntryRequest) =>
  getApi().post<WishlistEntryCreated>("/api/wishlist", payload);

export const updateWishlistEntry = (
  id: string,
  payload: UpdateWishlistEntryRequest,
) => getApi().put<void>(`/api/wishlist/${id}`, payload);

export const removeWishlistEntry = (id: string) =>
  getApi().del(`/api/wishlist/${id}`);
