import { describe, expect, it } from "vitest";
import { resolveApiBaseUrl } from "./index";

describe("resolveApiBaseUrl", () => {
  it("returns a trimmed API base URL", () => {
    const value = resolveApiBaseUrl({
      VITE_API_URL: "  https://api.test/v1  ",
    });

    expect(value).toBe("https://api.test/v1");
  });

  it("falls back to the current origin when VITE_API_URL is missing", () => {
    expect(resolveApiBaseUrl({ VITE_API_URL: "" })).toBe(
      window.location.origin,
    );
  });
});
