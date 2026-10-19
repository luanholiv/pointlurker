import { describe, it, expect, beforeEach } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import { fetchRedemptions } from "./redemptionsApi";
import { setAccessToken, clearAccessToken } from "../auth/authStorage";

describe("redemptionsApi", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  afterEach(() => {
    clearAccessToken();
  });

  describe("fetchRedemptions", () => {
    it("returns empty list with defaults", async () => {
      const result = await fetchRedemptions();
      expect(result.items).toEqual([]);
      expect(result.totalCount).toBe(0);
      expect(result.offset).toBe(0);
      expect(result.limit).toBe(20);
    });

    it("passes status filter in query string", async () => {
      let capturedUrl = "";
      server.use(
        http.get("http://localhost/api/redemptions", ({ request }) => {
          capturedUrl = request.url;
          return HttpResponse.json({
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 20,
          });
        }),
      );
      await fetchRedemptions({ status: "Completed" });
      expect(capturedUrl).toContain("status=Completed");
    });

    it("passes date range filters in query string", async () => {
      let capturedUrl = "";
      server.use(
        http.get("http://localhost/api/redemptions", ({ request }) => {
          capturedUrl = request.url;
          return HttpResponse.json({
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 20,
          });
        }),
      );
      await fetchRedemptions({ from: "2026-01-01", to: "2026-12-31" });
      expect(capturedUrl).toContain("from=2026-01-01");
      expect(capturedUrl).toContain("to=2026-12-31");
    });

    it("passes wishlistEntryId filter in query string", async () => {
      let capturedUrl = "";
      server.use(
        http.get("http://localhost/api/redemptions", ({ request }) => {
          capturedUrl = request.url;
          return HttpResponse.json({
            items: [],
            totalCount: 0,
            page: 1,
            pageSize: 20,
          });
        }),
      );
      await fetchRedemptions({ wishlistEntryId: "entry-42" });
      expect(capturedUrl).toContain("wishlistEntryId=entry-42");
    });

    it("passes pagination parameters", async () => {
      let capturedUrl = "";
      server.use(
        http.get("http://localhost/api/redemptions", ({ request }) => {
          capturedUrl = request.url;
          return HttpResponse.json({
            items: [],
            totalCount: 0,
            offset: 10,
            limit: 10,
          });
        }),
      );
      await fetchRedemptions({ page: 2, pageSize: 10 });
      // The pager stays a client concern: page 2 of 10 is offset 10.
      expect(capturedUrl).toContain("offset=10");
      expect(capturedUrl).toContain("limit=10");
    });

    it("returns items when API responds with data", async () => {
      const item = {
        id: "r-1",
        status: "Completed" as const,
        storeItemName: "VIP Badge",
        storeName: "xQc Store",
        attemptedAt: "2026-01-01T00:00:00Z",
        submittedAt: "2026-01-01T00:00:01Z",
        settledAt: "2026-01-01T00:00:02Z",
        failureCode: null,
        failureContext: null,
        pollFailures: 0,
        provider: "streamelements" as const,
      };
      server.use(
        http.get("http://localhost/api/redemptions", () =>
          HttpResponse.json({
            items: [item],
            totalCount: 1,
            page: 1,
            pageSize: 20,
          }),
        ),
      );
      const result = await fetchRedemptions();
      expect(result.items).toHaveLength(1);
      expect(result.items[0].id).toBe("r-1");
      expect(result.items[0].status).toBe("Completed");
    });

    it("throws on 401", async () => {
      server.use(
        http.get(
          "http://localhost/api/redemptions",
          () => new HttpResponse(null, { status: 401 }),
        ),
      );
      await expect(fetchRedemptions()).rejects.toThrow();
    });

    it("throws on 500", async () => {
      server.use(
        http.get(
          "http://localhost/api/redemptions",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      await expect(fetchRedemptions()).rejects.toThrow();
    });
  });
});
