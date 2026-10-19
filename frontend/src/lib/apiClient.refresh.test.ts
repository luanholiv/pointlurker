import { describe, it, expect, beforeEach, vi } from "vitest";
import {
  createApiClient,
  subscribeLogout,
  __resetRefreshState,
} from "./apiClient";
import {
  setTokenSet,
  getTokenSet,
  getAccessToken,
} from "../features/auth/authStorage";

const BASE = "http://localhost";

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  });

const accessTokenBody = (accessToken: string) => ({
  accessToken,
  refreshToken: "refresh-2",
  tokenType: "Bearer",
  expiresIn: 3600,
});

describe("apiClient auth-aware refresh", () => {
  beforeEach(() => {
    localStorage.clear();
    __resetRefreshState();
  });

  it("injects the bearer token from storage", async () => {
    setTokenSet({ accessToken: "abc", refreshToken: "r", expiresIn: 3600 });
    let authHeader: string | undefined;
    const fetcher = vi.fn(
      async (url: string | URL | Request, init?: RequestInit) => {
        void url;
        authHeader = (init?.headers as Record<string, string> | undefined)
          ?.Authorization;
        return json({ ok: true });
      },
    );
    const client = createApiClient({ baseUrl: BASE, fetcher });

    await client.get("/api/stores");

    expect(authHeader).toBe("Bearer abc");
  });

  it("on 401 refreshes once and retries the original request", async () => {
    setTokenSet({ accessToken: "old", refreshToken: "r", expiresIn: 3600 });
    let storesCalls = 0;
    const fetcher = vi.fn(async (url: string | URL | Request) => {
      const u = String(url);
      if (u.endsWith("/api/auth/refresh")) return json(accessTokenBody("new"));
      storesCalls += 1;
      return storesCalls === 1 ? json({}, 401) : json({ ok: true });
    });
    const client = createApiClient({ baseUrl: BASE, fetcher });

    const result = await client.get<{ ok: boolean }>("/api/stores");

    expect(result).toEqual({ ok: true });
    expect(storesCalls).toBe(2);
    expect(getAccessToken()).toBe("new"); // new pair persisted
  });

  it("shares a single refresh across concurrent 401s", async () => {
    setTokenSet({ accessToken: "old", refreshToken: "r", expiresIn: 3600 });
    let refreshCalls = 0;
    const fetcher = vi.fn(
      async (url: string | URL | Request, init?: RequestInit) => {
        const u = String(url);
        if (u.endsWith("/api/auth/refresh")) {
          refreshCalls += 1;
          await new Promise((r) => setTimeout(r, 5));
          return json(accessTokenBody("new"));
        }
        // First hit for each resource 401s; retries (with new token) succeed.
        const header = (init?.headers as Record<string, string>)?.Authorization;
        return header === "Bearer new" ? json({ ok: true }) : json({}, 401);
      },
    );
    const client = createApiClient({ baseUrl: BASE, fetcher });

    const [a, b] = await Promise.all([
      client.get("/api/stores"),
      client.get("/api/wishlist"),
    ]);

    expect(a).toEqual({ ok: true });
    expect(b).toEqual({ ok: true });
    expect(refreshCalls).toBe(1); // single-flight
  });

  it("clears storage and emits logout when refresh fails, without retrying", async () => {
    setTokenSet({ accessToken: "old", refreshToken: "r", expiresIn: 3600 });
    let storesCalls = 0;
    const fetcher = vi.fn(async (url: string | URL | Request) => {
      const u = String(url);
      if (u.endsWith("/api/auth/refresh")) return json({}, 401);
      storesCalls += 1;
      return json({}, 401);
    });
    const client = createApiClient({ baseUrl: BASE, fetcher });
    const onLogout = vi.fn();
    const unsub = subscribeLogout(onLogout);

    await expect(client.get("/api/stores")).rejects.toThrow();

    expect(storesCalls).toBe(1); // no retry after failed refresh
    expect(getTokenSet()).toBeNull(); // storage cleared
    expect(onLogout).toHaveBeenCalledTimes(1);
    unsub();
  });

  it("passes non-401 errors through without refreshing", async () => {
    setTokenSet({ accessToken: "old", refreshToken: "r", expiresIn: 3600 });
    let refreshCalls = 0;
    const fetcher = vi.fn(async (url: string | URL | Request) => {
      if (String(url).endsWith("/api/auth/refresh")) {
        refreshCalls += 1;
        return json(accessTokenBody("new"));
      }
      return json({}, 500);
    });
    const client = createApiClient({ baseUrl: BASE, fetcher });

    await expect(client.get("/api/stores")).rejects.toThrow();
    expect(refreshCalls).toBe(0);
  });
});
