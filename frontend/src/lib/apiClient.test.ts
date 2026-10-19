import { describe, expect, it, vi } from "vitest";
import { createApiClient } from "./apiClient";

describe("createApiClient", () => {
  it("prefixes GET requests with the base URL", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: async () => JSON.stringify({ status: "ok" }),
    });

    const client = createApiClient({
      baseUrl: "https://api.example.com",
      fetcher,
    });

    await client.get("/health");

    expect(fetcher).toHaveBeenCalledWith(
      "https://api.example.com/health",
      expect.objectContaining({ method: "GET" }),
    );
  });

  // Identity's register answers 200 with no body at all, not 204. Parsing that as JSON throws
  // "Unexpected end of JSON input" — a success reported as a failure.
  it("resolves an empty 200 body instead of failing to parse it", async () => {
    const fetcher = vi.fn().mockResolvedValue({
      ok: true,
      status: 200,
      text: async () => "",
    });

    const client = createApiClient({
      baseUrl: "https://api.example.com",
      fetcher,
    });

    await expect(
      client.post("/api/auth/register", {}),
    ).resolves.toBeUndefined();
  });
});
