import type { Page, Route } from "@playwright/test";

/**
 * Stub shapes for the list endpoints, which the server now filters, sorts and pages.
 *
 * Every one of them answers with the same envelope, and two of the values the UI shows are no
 * longer derivable from a list the client holds: the store list's active/paused counts and a
 * store's item-grid facets are separate endpoints, because they must stay constant while a filter
 * narrows the list. Route stubs that skip them leave those surfaces unstubbed, so they are bundled
 * into the helpers here rather than left to each spec to remember.
 */

export type Envelope<TRow> = {
  items: TRow[];
  totalCount: number;
  offset: number;
  limit: number;
};

/** Wraps rows as a single full page — the shape every list endpoint answers with. */
export const pageOf = <TRow>(
  rows: TRow[],
  limit = Math.max(rows.length, 1),
): Envelope<TRow> => ({
  items: rows,
  totalCount: rows.length,
  offset: 0,
  limit,
});

const json = (route: Route, body: unknown, status = 200) =>
  route.fulfill({
    status,
    contentType: "application/json",
    body: JSON.stringify(body),
  });

export const fulfillJson = json;

/**
 * `GET /api/stores/summary` — the headline active/paused counts. Derived from the rows by default,
 * since a spec that cares about the numbers is a spec that set the rows.
 */
export async function stubStoreSummary(
  page: Page,
  stores: { isActive: boolean }[],
): Promise<void> {
  await page.route("**/api/stores/summary", (route) =>
    json(route, {
      activeCount: stores.filter((s) => s.isActive).length,
      pausedCount: stores.filter((s) => !s.isActive).length,
    }),
  );
}

/** The fields the item query narrows and orders on. Everything else rides along untouched. */
type StubItem = {
  id: string;
  name: string;
  categoryName: string;
  cost: number;
  isAvailable: boolean;
  subscriberOnly: boolean;
  featured: boolean;
  quantityCurrent: number;
  providerCreatedAt?: string;
};

/**
 * Applies the item query the way PostgreSQL does, from the query string alone.
 *
 * Reading the filter out of the URL rather than out of any client state is the point: a spec that
 * asserts "only Armor items are on screen" then proves the browser asked for Armor, not that it
 * re-filtered rows it already had. The two stock rules the backend carries are mirrored here —
 * `avail` defaults ON when the parameter is absent, and `-1` means unlimited, so it sorts as the
 * largest stock rather than the smallest.
 *
 * `q` models the name arms only. These stub rows carry no description, so there is nothing for the
 * server's description matching or its relevance tiers to act on and an unrecognised `sort` falls
 * to name order, which is the same answer. The real matching is proven against real PostgreSQL in
 * `IntegrationTests/Stores/StoreItemSearchTests` and end to end in `store-item-search.real.spec.ts`.
 */
function applyItemQuery<TItem extends StubItem>(
  items: TItem[],
  params: URLSearchParams,
): TItem[] {
  const q = params.get("q")?.toLowerCase();
  const categories = params.get("cat")?.split(",").filter(Boolean) ?? [];
  const min = params.get("min");
  const max = params.get("max");

  const matched = items.filter((item) => {
    if (params.get("avail") !== "0" && !item.isAvailable) return false;
    if (params.get("sub") === "1" && !item.subscriberOnly) return false;
    if (params.get("feat") === "1" && !item.featured) return false;
    if (categories.length > 0 && !categories.includes(item.categoryName))
      return false;
    if (min !== null && item.cost < Number(min)) return false;
    if (max !== null && item.cost > Number(max)) return false;
    if (q && !item.name.toLowerCase().includes(q)) return false;
    return true;
  });

  const descending = params.get("dir") === "desc";
  const flip = descending ? -1 : 1;

  const primary = (a: TItem, b: TItem): number => {
    switch (params.get("sort")) {
      case "cost":
        return a.cost - b.cost;
      case "stock": {
        const unlimited = (i: TItem) => (i.quantityCurrent === -1 ? 1 : 0);
        return (
          unlimited(a) - unlimited(b) || a.quantityCurrent - b.quantityCurrent
        );
      }
      case "date":
        return (a.providerCreatedAt ?? "").localeCompare(
          b.providerCreatedAt ?? "",
        );
      default:
        return a.name.toLowerCase().localeCompare(b.name.toLowerCase());
    }
  };

  // The id tiebreaker stays ascending even when the primary key descends, so paging over ties
  // neither repeats nor skips a row.
  return [...matched].sort(
    (a, b) => primary(a, b) * flip || a.id.localeCompare(b.id),
  );
}

/**
 * `GET /api/stores/{id}/items` and its facets. The facets are computed across every item, not the
 * matched ones, so the category chips and cost bounds hold still while a filter narrows the grid.
 */
export async function stubStoreItems<TItem extends StubItem>(
  page: Page,
  storeId: string,
  items: TItem[],
): Promise<void> {
  // One handler for both routes: relying on a glob to tell `/items` from `/items/facets` means
  // relying on how Playwright treats `?` in a URL pattern, which is not worth depending on.
  await page.route(`**/api/stores/${storeId}/items**`, (route) => {
    const url = new URL(route.request().url());

    if (url.pathname.endsWith("/facets")) {
      return json(route, {
        categories: [...new Set(items.map((item) => item.categoryName))].sort(),
        minCost: items.length ? Math.min(...items.map((i) => i.cost)) : 0,
        maxCost: items.length ? Math.max(...items.map((i) => i.cost)) : 0,
      });
    }

    const matched = applyItemQuery(items, url.searchParams);
    const offset = Number(url.searchParams.get("offset") ?? 0);
    const limit = Number(url.searchParams.get("limit") ?? matched.length);

    return json(route, {
      items: matched.slice(offset, offset + limit),
      totalCount: matched.length,
      offset,
      limit,
    });
  });
}

/** The columns the redemption history orders by, as the request names them. */
type RedemptionRow = {
  id: string;
  status: string;
  storeItemName: string;
  storeName: string;
  attemptedAt: string;
  submittedAt: string | null;
  settledAt: string | null;
};

/**
 * `GET /api/redemptions`, ordered and sliced from the query string.
 *
 * The history is the surface where client-side sorting was outright wrong: it reordered the twenty
 * rows on screen and presented that as a sorted history. Serving from the whole set here is what
 * lets a spec tell the two apart — a sort issued from a later page can surface a row that page
 * never held.
 */
export async function stubRedemptions<TRow extends RedemptionRow>(
  page: Page,
  rows: TRow[],
): Promise<void> {
  await page.route("**/api/redemptions**", (route) => {
    const params = new URL(route.request().url()).searchParams;
    const status = params.get("status");
    const matched = rows.filter((row) => !status || row.status === status);

    const field = (params.get("sort") ?? "attemptedAt") as keyof RedemptionRow;
    const flip = params.get("dir") === "desc" ? -1 : 1;
    const ordered = [...matched].sort(
      (a, b) =>
        String(a[field] ?? "").localeCompare(String(b[field] ?? "")) * flip ||
        a.id.localeCompare(b.id),
    );

    const offset = Number(params.get("offset") ?? 0);
    const limit = Number(params.get("limit") ?? ordered.length);

    return json(route, {
      items: ordered.slice(offset, offset + limit),
      totalCount: ordered.length,
      offset,
      limit,
    });
  });
}
