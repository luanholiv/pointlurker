import { describe, it, expect, beforeEach } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import {
  fetchWishlistGrouped,
  fetchWishlistEntry,
  addWishlistEntry,
  updateWishlistEntry,
  removeWishlistEntry,
} from "./wishlistApi";
import { setAccessToken, clearAccessToken } from "../auth/authStorage";

/** Only `items` varies between these fixtures, so the rest of the envelope is spread in. */
const pageMeta = { totalCount: 0, offset: 0, limit: 0 };

describe("wishlistApi", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  afterEach(() => {
    clearAccessToken();
  });

  describe("fetchWishlistGrouped", () => {
    it("returns grouped list with empty stores by default", async () => {
      const result = await fetchWishlistGrouped();
      expect(result.items).toEqual([]);
    });

    it("returns actual store groups when handler responds with data", async () => {
      server.use(
        http.get("http://localhost/api/wishlist", () =>
          HttpResponse.json({
            ...pageMeta,
            items: [
              {
                storeId: "store-1",
                storeName: "xQc Store",
                isActive: true,
                lastSyncedAt: "2026-01-01T00:00:00Z",
                entries: [],
              },
            ],
          }),
        ),
      );
      const result = await fetchWishlistGrouped();
      expect(result.items).toHaveLength(1);
      expect(result.items[0].storeName).toBe("xQc Store");
    });

    it("throws on 401", async () => {
      server.use(
        http.get(
          "http://localhost/api/wishlist",
          () => new HttpResponse(null, { status: 401 }),
        ),
      );
      await expect(fetchWishlistGrouped()).rejects.toThrow();
    });

    it("throws on 500", async () => {
      server.use(
        http.get(
          "http://localhost/api/wishlist",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      await expect(fetchWishlistGrouped()).rejects.toThrow();
    });
  });

  describe("fetchWishlistEntry", () => {
    it("returns entry detail", async () => {
      const result = await fetchWishlistEntry("entry-1");
      expect(result.id).toBe("entry-1");
      expect(result.itemName).toBe("Test Item");
    });

    it("throws on 404", async () => {
      server.use(
        http.get(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 404 }),
        ),
      );
      await expect(fetchWishlistEntry("missing")).rejects.toThrow();
    });
  });

  describe("addWishlistEntry", () => {
    it("returns created entry on success", async () => {
      const result = await addWishlistEntry({
        storeItemId: "item-1",
        notifyOnAvailable: true,
        autoRedeem: false,
      });
      expect(result.id).toBe("entry-1");
      expect(result.itemName).toBe("Test Item");
    });

    it("throws on 400 validation error", async () => {
      server.use(
        http.post(
          "http://localhost/api/wishlist",
          () => new HttpResponse(null, { status: 400 }),
        ),
      );
      await expect(
        addWishlistEntry({
          storeItemId: "",
          notifyOnAvailable: false,
          autoRedeem: false,
        }),
      ).rejects.toThrow();
    });
  });

  describe("updateWishlistEntry", () => {
    it("resolves on success (204)", async () => {
      await expect(
        updateWishlistEntry("entry-1", {
          notifyOnAvailable: false,
          autoRedeem: false,
        }),
      ).resolves.toBeUndefined();
    });

    it("throws on 404", async () => {
      server.use(
        http.put(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 404 }),
        ),
      );
      await expect(
        updateWishlistEntry("missing", {
          notifyOnAvailable: false,
          autoRedeem: false,
        }),
      ).rejects.toThrow();
    });
  });

  describe("removeWishlistEntry", () => {
    it("resolves on success (204)", async () => {
      await expect(removeWishlistEntry("entry-1")).resolves.toBeUndefined();
    });

    it("throws on 404", async () => {
      server.use(
        http.delete(
          "http://localhost/api/wishlist/:id",
          () => new HttpResponse(null, { status: 404 }),
        ),
      );
      await expect(removeWishlistEntry("missing")).rejects.toThrow();
    });
  });
});
