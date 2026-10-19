import { http, HttpResponse } from "msw";

const BASE = "http://localhost";

export const wishlistHandlers = [
  // The shared page envelope, where one row is a store group.
  http.get(`${BASE}/api/wishlist`, () =>
    HttpResponse.json({ items: [], totalCount: 0, offset: 0, limit: 0 }),
  ),

  http.get(`${BASE}/api/wishlist/:id`, () =>
    HttpResponse.json({
      id: "entry-1",
      storeItemId: "item-1",
      itemName: "Test Item",
      itemCost: 100,
      isAvailable: true,
      storeName: "Test Store",
      storeId: "store-1",
      notifyOnAvailable: true,
      autoRedeem: false,
      desiredRedemptions: 1,
      quotaUsed: 0,
      userInputValues: [],
      userInputDefinitions: [],
      allowMessages: false,
      redemptionMessage: null,
      hasStaleInputs: false,
      createdAt: "2026-01-01T00:00:00Z",
      updatedAt: "2026-01-01T00:00:00Z",
    }),
  ),

  http.post(`${BASE}/api/wishlist`, () =>
    HttpResponse.json(
      {
        id: "entry-1",
        storeItemId: "item-1",
        itemName: "Test Item",
        storeName: "Test Store",
        notifyOnAvailable: true,
        autoRedeem: false,
        createdAt: "2026-01-01T00:00:00Z",
      },
      { status: 201 },
    ),
  ),

  http.put(
    `${BASE}/api/wishlist/:id`,
    () => new HttpResponse(null, { status: 204 }),
  ),

  http.delete(
    `${BASE}/api/wishlist/:id`,
    () => new HttpResponse(null, { status: 204 }),
  ),
];
