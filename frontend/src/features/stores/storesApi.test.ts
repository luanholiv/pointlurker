import { afterEach, beforeEach } from "vitest";
import {
  createStore,
  deactivateStore,
  deleteStoreProfileAlias,
  deleteStoreProfileValue,
  fetchStoreDetail,
  fetchStoreProfile,
  fetchStores,
  lookupStore,
  removeStore,
} from "./storesApi";

vi.mock("../auth/authStorage", () => ({
  getAccessToken: vi.fn(() => "test-token"),
}));

vi.mock("../../config", () => ({
  getAppConfig: vi.fn(() => ({ apiBaseUrl: "http://test.local" })),
}));

import { getAccessToken } from "../auth/authStorage";
const mockGetAccessToken = vi.mocked(getAccessToken);

// `text` is what the client reads on a success, because not every 200 carries a document.
const okResponse = (data: unknown) =>
  Promise.resolve({
    ok: true,
    status: 200,
    json: async () => data,
    text: async () => JSON.stringify(data),
  } as Response);

const errorResponse = (
  status: number,
  body: unknown = {},
  contentType = "application/json",
) =>
  Promise.resolve({
    ok: false,
    status,
    headers: {
      get: (name: string) =>
        name.toLowerCase() === "content-type" ? contentType : null,
    },
    json: async () => body,
  } as unknown as Response);

describe("storesApi", () => {
  let fetchSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    mockGetAccessToken.mockReturnValue("test-token");
    fetchSpy = vi.spyOn(globalThis, "fetch");
  });

  afterEach(() => {
    fetchSpy.mockRestore();
  });

  describe("fetchStores", () => {
    it("calls GET /api/stores with auth header", async () => {
      fetchSpy.mockReturnValueOnce(okResponse([]));

      const result = await fetchStores();

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores",
        expect.objectContaining({
          method: "GET",
          headers: expect.objectContaining({
            Authorization: "Bearer test-token",
          }),
        }),
      );
      expect(result).toEqual([]);
    });
  });

  describe("fetchStoreDetail", () => {
    it("calls GET /api/stores/:id", async () => {
      const store = {
        id: "store-1",
        channelId: "ch1",
        displayName: "Store",
        isActive: true,
        lastSyncedAt: null,
        createdAt: "",
        items: [],
      };
      fetchSpy.mockReturnValueOnce(okResponse(store));

      const result = await fetchStoreDetail("store-1");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/store-1",
        expect.objectContaining({ method: "GET" }),
      );
      expect(result).toEqual(store);
    });
  });

  describe("createStore", () => {
    it("calls POST /api/stores with payload", async () => {
      const created = { id: "store-1" };
      fetchSpy.mockReturnValueOnce(okResponse(created));

      const result = await createStore({
        provider: "streamelements",
        channelId: "ch1",
        displayName: "Test",
      });

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({
            provider: "streamelements",
            channelId: "ch1",
            displayName: "Test",
          }),
        }),
      );
      expect(result).toEqual(created);
    });
  });

  describe("deactivateStore", () => {
    it("calls PATCH /api/stores/:id/deactivate", async () => {
      fetchSpy.mockReturnValueOnce(okResponse({}));

      await deactivateStore("store-1");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/store-1/deactivate",
        expect.objectContaining({ method: "PATCH" }),
      );
    });
  });

  describe("removeStore", () => {
    it("calls DELETE /api/stores/:id", async () => {
      fetchSpy.mockReturnValueOnce(okResponse({}));

      await removeStore("store-1");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/store-1",
        expect.objectContaining({ method: "DELETE" }),
      );
    });
  });

  describe("fetchStoreProfile", () => {
    it("calls GET /api/stores/:id/profile and returns the unified payload", async () => {
      const profile = {
        unresolved: [
          {
            rawLabel: "Nome no jogo",
            bestCanonical: "Twitch Username",
            bestScore: 72,
            aliasTarget: null,
            itemCount: 3,
            blockedEntryCount: 2,
          },
        ],
        resolved: [
          {
            labelCanonical: "Twitch Username",
            updatedAt: "2026-07-12T10:04:00Z",
            requestedBy: [
              {
                rawLabel: "Twitch Username",
                source: "Exact",
                confidence: 100,
                itemCount: 5,
              },
            ],
          },
        ],
        canonicalLabels: ["Twitch Username"],
      };
      fetchSpy.mockReturnValueOnce(okResponse(profile));

      const result = await fetchStoreProfile("store-1");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/store-1/profile",
        expect.objectContaining({ method: "GET" }),
      );
      expect(result).toEqual(profile);
    });
  });

  describe("deleteStoreProfileValue", () => {
    it("calls DELETE /profile/values with the label in the query string", async () => {
      fetchSpy.mockReturnValueOnce(okResponse({}));

      await deleteStoreProfileValue("store-1", "Steam ID");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/store-1/profile/values?label=Steam%20ID",
        expect.objectContaining({ method: "DELETE" }),
      );
    });

    it("encodes slashes and parentheses in the label", async () => {
      fetchSpy.mockReturnValueOnce(okResponse({}));

      await deleteStoreProfileValue("store-1", "Discord tag / handle (IGN)");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/store-1/profile/values?label=Discord%20tag%20%2F%20handle%20(IGN)",
        expect.anything(),
      );
    });
  });

  describe("deleteStoreProfileAlias", () => {
    it("calls DELETE /profile/aliases with the raw label in the query string", async () => {
      fetchSpy.mockReturnValueOnce(okResponse({}));

      await deleteStoreProfileAlias("store-1", "twitchh usrname");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/store-1/profile/aliases?rawLabel=twitchh%20usrname",
        expect.objectContaining({ method: "DELETE" }),
      );
    });
  });

  describe("lookupStore", () => {
    it("calls GET /api/stores/lookup with the provider and the encoded query", async () => {
      const store = {
        externalId: "ch1",
        handle: "user name",
        displayName: "User",
        avatarUrl: null,
        itemCount: null,
      };
      fetchSpy.mockReturnValueOnce(okResponse(store));

      const result = await lookupStore("streamelements", "user name");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/lookup?provider=streamelements&query=user+name",
        expect.objectContaining({ method: "GET" }),
      );
      expect(result).toEqual(store);
    });

    it("sends no query for a fixed-store provider", async () => {
      fetchSpy.mockReturnValueOnce(okResponse({}));

      await lookupStore("gaupoints");

      expect(fetchSpy).toHaveBeenCalledWith(
        "http://test.local/api/stores/lookup?provider=gaupoints",
        expect.anything(),
      );
    });

    it("throws on non-ok response", async () => {
      fetchSpy.mockReturnValueOnce(errorResponse(404));

      await expect(lookupStore("streamelements", "nobody")).rejects.toThrow(
        "Request failed with status 404",
      );
    });
  });
});
