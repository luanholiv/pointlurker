import { describe, it, expect, vi, beforeEach } from "vitest";
import { ApiError } from "../lib/apiClient";

// Mock the i18n instance before importing errorCatalog
const mockT = vi.fn<(key: string, params?: unknown) => string>();
vi.mock("./index", () => ({
  default: { t: mockT },
}));

// Import after mock is set up
const { tError, tApiError } = await import("./errorCatalog");

describe("tError", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("returns translated string for known error code", () => {
    const key = "errors.validation.failed";
    mockT.mockImplementation((k) => (k === key ? "Validation failed" : k));

    const result = tError("validation.failed");

    expect(result).toBe("Validation failed");
  });

  it("returns titleFallback for unknown code when fallback provided", () => {
    // i18next returns the key itself when no translation found
    mockT.mockImplementation((k: string) => k);

    const result = tError(
      "some.unknown.code",
      undefined,
      "Something went wrong",
    );

    expect(result).toBe("Something went wrong");
  });

  it("returns the code itself for unknown code when no fallback", () => {
    mockT.mockImplementation((k: string) => k);

    const result = tError("some.unknown.code");

    expect(result).toBe("some.unknown.code");
  });

  it("passes params to i18n.t", () => {
    const key = "errors.store.channel_not_found";
    mockT.mockImplementation((k, p) =>
      k === key
        ? `Channel '${(p as Record<string, string>).username}' not found`
        : k,
    );

    const result = tError("store.channel_not_found", { username: "testuser" });

    expect(result).toBe("Channel 'testuser' not found");
    expect(mockT).toHaveBeenCalledWith(key, { username: "testuser" });
  });

  it("logs a console.warn for unknown codes (DEV mode is always active in vitest)", () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockT.mockImplementation((k: string) => k);

    tError("unknown.code", undefined, "fallback");

    expect(warnSpy).toHaveBeenCalledWith(
      expect.stringContaining("unknown.code"),
    );

    warnSpy.mockRestore();
  });
});

describe("tApiError", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("maps an ApiError's code through the catalog", () => {
    const key = "errors.wishlist.entry_not_found";
    mockT.mockImplementation((k) =>
      k === key ? "Wishlist entry not found." : k,
    );

    const result = tApiError(
      new ApiError({ code: "wishlist.entry_not_found", status: 404 }),
      "wishlist.toast.loadEntryError",
    );

    expect(result).toBe("Wishlist entry not found.");
  });

  it("interpolates the ApiError's params", () => {
    const key = "errors.stores.already_exists";
    mockT.mockImplementation((k, p) =>
      k === key
        ? `A store for channel '${(p as Record<string, string>).channelId}' already exists.`
        : k,
    );

    const result = tApiError(
      new ApiError({
        code: "stores.already_exists",
        params: { channelId: "ch-1" },
        status: 409,
      }),
      "stores.toast.createError",
    );

    expect(result).toBe("A store for channel 'ch-1' already exists.");
  });

  it("interpolates the params of a per-field entry", () => {
    const key = "errors.validation.too_long";
    mockT.mockImplementation((k, p) =>
      k === key
        ? `Value is too long (maximum ${(p as Record<string, string>).max} characters).`
        : k,
    );

    const result = tApiError(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "redemptionMessage",
              code: "validation.too_long",
              params: { field: "redemptionMessage", max: 1000 },
            },
          ],
        },
        status: 400,
      }),
      "wishlist.toast.loadEntryError",
    );

    expect(result).toBe("Value is too long (maximum 1000 characters).");
  });

  it("falls back to the ApiError's title when the code is unmapped", () => {
    const warnSpy = vi.spyOn(console, "warn").mockImplementation(() => {});
    mockT.mockImplementation((k: string) => k);

    const result = tApiError(
      new ApiError({
        code: "wishlist.brand_new_code",
        status: 400,
        title: "Brand new failure",
      }),
      "wishlist.toast.loadEntryError",
    );

    expect(result).toBe("Brand new failure");
    warnSpy.mockRestore();
  });

  // A plain Error is what apiClient throws for a response that is not problem+json
  // ("Request failed with status 500", "Session expired"). Its message is a developer
  // string, so the caller's fallback key is what the user actually reads.
  it("uses the fallback key for a non-ApiError Error", () => {
    const key = "wishlist.toast.loadEntryError";
    mockT.mockImplementation((k) =>
      k === key ? "Failed to load wishlist entry" : k,
    );

    const result = tApiError(new Error("Request failed with status 500"), key);

    expect(result).toBe("Failed to load wishlist entry");
    expect(mockT).toHaveBeenCalledWith(key);
  });

  // A dropped connection surfaces as a raw TypeError out of fetch, never an ApiError.
  it("uses the fallback key for a raw fetch TypeError", () => {
    const key = "wishlist.toast.removeError";
    mockT.mockImplementation((k) =>
      k === key ? "Failed to remove wishlist entry" : k,
    );

    const result = tApiError(new TypeError("Failed to fetch"), key);

    expect(result).toBe("Failed to remove wishlist entry");
  });
});

/**
 * A validation failure names the rule that broke in `params.errors[]`, one entry per field with its
 * own code and params. Resolving only the outer code renders "One or more validation errors
 * occurred." — true, and useless to whoever has to fix the input.
 *
 * The unwrap is driven by the shape rather than by a specific outer code, so it serves any endpoint
 * that reports per-field failures, and falls straight back to the outer code when the nested one is
 * not in the catalog.
 */
describe("tApiError — per-field validation errors", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  const validationFailure = (errors: unknown) =>
    new ApiError({
      code: "validation.failed",
      params: { errors },
      status: 400,
      title: "One or more validation errors occurred.",
    });

  it("resolves the first field error's own code and params", () => {
    mockT.mockImplementation((k, p) =>
      k === "errors.validation.password_too_short"
        ? `Password must be at least ${(p as { min: number }).min} characters.`
        : k,
    );

    const result = tApiError(
      validationFailure([
        {
          field: "password",
          code: "validation.password_too_short",
          params: { min: 8 },
        },
      ]),
      "settings.updateFailed",
    );

    expect(result).toBe("Password must be at least 8 characters.");
  });

  it("shows the first entry when several rules failed at once", () => {
    mockT.mockImplementation((k) =>
      k === "errors.validation.password_requires_digit"
        ? "Password must contain at least one digit."
        : k,
    );

    const result = tApiError(
      validationFailure([
        { field: "password", code: "validation.password_requires_digit" },
        { field: "password", code: "validation.password_requires_upper" },
      ]),
      "settings.updateFailed",
    );

    expect(result).toBe("Password must contain at least one digit.");
  });

  it("falls back to the outer code when the nested code is not in the catalog", () => {
    mockT.mockImplementation((k) =>
      k === "errors.validation.failed"
        ? "One or more validation errors occurred."
        : k,
    );

    const result = tApiError(
      validationFailure([{ field: "password", code: "validation.brand_new" }]),
      "settings.updateFailed",
    );

    expect(result).toBe("One or more validation errors occurred.");
  });

  it("falls back to the outer code when the errors array is empty", () => {
    mockT.mockImplementation((k) =>
      k === "errors.validation.failed"
        ? "One or more validation errors occurred."
        : k,
    );

    const result = tApiError(validationFailure([]), "settings.updateFailed");

    expect(result).toBe("One or more validation errors occurred.");
  });

  it("ignores an errors param that is not an array of coded entries", () => {
    mockT.mockImplementation((k) =>
      k === "errors.search.invalid_enum_value"
        ? "That value is not allowed."
        : k,
    );

    const result = tApiError(
      new ApiError({
        code: "search.invalid_enum_value",
        params: { parameter: "sort", errors: "not-an-array" },
        status: 400,
        title: "Invalid enum value",
      }),
      "stores.loadError",
    );

    expect(result).toBe("That value is not allowed.");
  });

  // A bad query filter arrives as a per-field entry, so the catalog sentence has to interpolate from
  // the *nested* params. This is what lets the backend carry `parameter`/`value` into the entry and
  // leave both locale catalogs untouched.
  it("interpolates a search failure from the nested entry params", () => {
    mockT.mockImplementation((k, params) => {
      if (k !== "errors.search.invalid_enum_value") {
        return k;
      }
      const { value, parameter } = params as {
        value: string;
        parameter: string;
      };
      return `'${value}' is not a valid value for '${parameter}'.`;
    });

    const result = tApiError(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "sort",
              code: "search.invalid_enum_value",
              params: {
                field: "sort",
                parameter: "sort",
                value: "bogus",
                allowed: ["Name", "Cost"],
              },
            },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
      "stores.loadError",
    );

    expect(result).toBe("'bogus' is not a valid value for 'sort'.");
  });

  // Two bad filters now come back together. The banner shows the first, which is the entry the
  // handler read first.
  it("renders the first entry when several filters were rejected", () => {
    mockT.mockImplementation((k) =>
      k === "errors.search.invalid_filter_value" ? "Bad filter." : k,
    );

    const result = tApiError(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "min",
              code: "search.invalid_filter_value",
              params: { field: "min", parameter: "min", value: "abc" },
            },
            {
              field: "sort",
              code: "search.invalid_enum_value",
              params: { field: "sort", parameter: "sort", value: "bogus" },
            },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
      "stores.loadError",
    );

    expect(result).toBe("Bad filter.");
  });
});
