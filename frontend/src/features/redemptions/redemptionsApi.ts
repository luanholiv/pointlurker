import type { StoreProvider } from "../stores/storeProviders";
import { getAppConfig } from "../../config";
import { createApiClient } from "../../lib/apiClient";

/**
 * The `RedemptionStatus` members, spelled once. The API sends the status as the enum member's own
 * name and the history page looks its label up under `redemption.status.<member>`, so this list is
 * the wire contract and the catalog keys at the same time — which is why it is a value rather than
 * only a type: `redemptionStatuses.test.ts` compares it against the catalog, and the backend's
 * `RedemptionStatusCatalogTests` compares the catalog against the enum.
 */
export const REDEMPTION_STATUSES = [
  "Pending",
  "Submitted",
  "Completed",
  "SubmissionFailed",
  "CompletionTimedOut",
  "Rejected",
] as const;

export type RedemptionStatus = (typeof REDEMPTION_STATUSES)[number];

export type RedemptionItem = {
  id: string;
  status: RedemptionStatus;
  storeItemName: string;
  storeName: string;
  attemptedAt: string;
  submittedAt: string | null;
  settledAt: string | null;
  /** A `redemption.failure.*` error code, resolved through the catalog by `tError`. */
  failureCode: string | null;
  failureContext: string | null;
  /**
   * Consecutive failed completion checks. Non-zero on a `Submitted` row means the outcome cannot
   * currently be read upstream — what tells a row still waiting apart from a wedged one.
   */
  pollFailures: number;
  /** Snapshotted on the redemption, so it outlives the store. */
  provider: StoreProvider;
};

/** The shared page envelope. Slice bounds are an offset and a limit, as on every other list. */
export type RedemptionListResponse = {
  items: RedemptionItem[];
  totalCount: number;
  offset: number;
  limit: number;
};

/** The columns the history presents, and therefore the fields it can be ordered by. */
export type RedemptionSortField =
  | "status"
  | "storeItemName"
  | "storeName"
  | "attemptedAt"
  | "submittedAt"
  | "settledAt";

export type RedemptionFilter = {
  status?: RedemptionStatus | null;
  wishlistEntryId?: string | null;
  from?: string | null;
  to?: string | null;
  sort?: RedemptionSortField;
  dir?: "asc" | "desc";
  /** The numbered pager is a client concern; it derives the offset from the page it presents. */
  page?: number;
  pageSize?: number;
};

// The auth-aware client injects the bearer token from storage and handles 401 refresh.
const getApi = () => createApiClient({ baseUrl: getAppConfig().apiBaseUrl });

export const fetchRedemptions = (filter: RedemptionFilter = {}) => {
  const params = new URLSearchParams();
  if (filter.status) params.set("status", filter.status);
  if (filter.wishlistEntryId)
    params.set("wishlistEntryId", filter.wishlistEntryId);
  if (filter.from) params.set("from", filter.from);
  if (filter.to) params.set("to", filter.to);
  if (filter.sort) params.set("sort", filter.sort);
  if (filter.dir) params.set("dir", filter.dir);

  // The history keeps its numbered pager, so the page number becomes an offset here rather than
  // the endpoint growing a second way to express a slice.
  const pageSize = filter.pageSize ?? 20;
  params.set("offset", String(((filter.page ?? 1) - 1) * pageSize));
  params.set("limit", String(pageSize));
  const qs = params.toString();
  return getApi().get<RedemptionListResponse>(`/api/redemptions?${qs}`);
};
