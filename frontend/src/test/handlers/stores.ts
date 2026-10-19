import { http, HttpResponse } from "msw";

const BASE = "http://localhost";

const emptyPage = { items: [], totalCount: 0, offset: 0, limit: 48 };

export const storesHandlers = [
  // Ahead of `/api/stores/:id`, which would otherwise match "lookup" as an id.
  http.get(`${BASE}/api/stores/lookup`, () =>
    HttpResponse.json({
      externalId: "ch-1",
      handle: "testuser",
      displayName: "TestUser",
      avatarUrl: null,
      itemCount: null,
    }),
  ),

  http.get(`${BASE}/api/stores`, () => HttpResponse.json(emptyPage)),

  http.get(`${BASE}/api/stores/summary`, () =>
    HttpResponse.json({ activeCount: 0, pausedCount: 0 }),
  ),

  // The store envelope carries no items: they are served filtered, sorted and paged by
  // /api/stores/{id}/items, and what stays here is the unfiltered count.
  http.get(`${BASE}/api/stores/:id`, () =>
    HttpResponse.json({
      id: "store-1",
      channelId: "ch-1",
      displayName: "Test Store",
      isActive: true,
      lastSyncedAt: null,
      createdAt: "2026-01-01T00:00:00Z",
      itemCount: 0,
      missingLabels: [],
    }),
  ),

  http.get(`${BASE}/api/stores/:id/items`, () => HttpResponse.json(emptyPage)),

  http.get(`${BASE}/api/stores/:id/items/facets`, () =>
    HttpResponse.json({ categories: [], minCost: 0, maxCost: 0 }),
  ),

  http.post(`${BASE}/api/stores`, () =>
    HttpResponse.json({ id: "store-1" }, { status: 201 }),
  ),

  http.patch(
    `${BASE}/api/stores/:id/deactivate`,
    () => new HttpResponse(null, { status: 204 }),
  ),

  http.patch(
    `${BASE}/api/stores/:id/activate`,
    () => new HttpResponse(null, { status: 204 }),
  ),

  http.delete(
    `${BASE}/api/stores/:id`,
    () => new HttpResponse(null, { status: 204 }),
  ),
];
