import { describe, it, expect, vi, beforeEach } from "vitest";
import { ApiError } from "../lib/apiClient";

const mockT = vi.fn<(key: string, params?: unknown) => string>();
vi.mock("./index", () => ({
  default: { t: mockT },
}));

const { tApiFieldErrors } = await import("./fieldErrors");

/** i18next answers an unknown key with the key itself; a known one gets a sentence. */
function catalog(entries: Record<string, string>) {
  return (key: string) => entries[key] ?? key;
}

function validationFailed(errors: unknown[]) {
  return new ApiError({
    code: "validation.failed",
    params: { errors },
    status: 400,
    title: "One or more validation errors occurred.",
  });
}

describe("tApiFieldErrors", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("returns nothing for a value that is not an ApiError", () => {
    expect(tApiFieldErrors(new Error("boom"))).toEqual([]);
  });

  it("returns nothing for an ApiError carrying no per-field entries", () => {
    const err = new ApiError({
      code: "stores.not_found",
      status: 404,
      title: "Not found",
    });

    expect(tApiFieldErrors(err)).toEqual([]);
  });

  it("returns nothing when errors is present but empty", () => {
    expect(tApiFieldErrors(validationFailed([]))).toEqual([]);
  });

  // Entries 1..n are the ones a resolver that answers with one string drops.
  it("resolves every entry, not only the first", () => {
    mockT.mockImplementation(
      catalog({ "errors.validation.required": "This field is required." }),
    );

    const result = tApiFieldErrors(
      validationFailed([
        { field: "channelId", code: "validation.required" },
        { field: "displayName", code: "validation.required" },
      ]),
    );

    expect(result).toEqual([
      { field: "channelId", message: "This field is required." },
      { field: "displayName", message: "This field is required." },
    ]);
  });

  it("interpolates each entry from its own params", () => {
    mockT.mockImplementation((key, params) =>
      key === "errors.validation.too_long"
        ? `Too long (max ${(params as { max: number }).max}).`
        : key,
    );

    const result = tApiFieldErrors(
      validationFailed([
        {
          field: "redemptionMessage",
          code: "validation.too_long",
          params: { field: "redemptionMessage", max: 1000 },
        },
      ]),
    );

    expect(result).toEqual([
      { field: "redemptionMessage", message: "Too long (max 1000)." },
    ]);
  });

  // IdentityErrorMapper builds its entries itself and does not repeat `field` inside their params,
  // unlike the producers that go through FieldErrorProblem. The entry's own `field` is the one key
  // all three populate.
  it("reads the field from the entry rather than from its params", () => {
    mockT.mockImplementation(
      catalog({
        "errors.validation.password_too_short": "Password is too short.",
      }),
    );

    const result = tApiFieldErrors(
      validationFailed([
        {
          field: "password",
          code: "validation.password_too_short",
          params: { min: 8 },
        },
      ]),
    );

    expect(result).toEqual([
      { field: "password", message: "Password is too short." },
    ]);
  });

  it("keeps a collection path intact so the caller can find the element", () => {
    mockT.mockImplementation(
      catalog({ "errors.validation.required": "This field is required." }),
    );

    const result = tApiFieldErrors(
      validationFailed([
        { field: "values[0].value", code: "validation.required" },
      ]),
    );

    expect(result[0].field).toBe("values[0].value");
  });

  // A rule added by a future .NET version must degrade to the wrapper's sentence, not to nothing.
  it("falls back to the wrapper's sentence for an entry whose code the catalog lacks", () => {
    mockT.mockImplementation(
      catalog({
        "errors.validation.failed": "One or more validation errors occurred.",
      }),
    );

    const result = tApiFieldErrors(
      validationFailed([{ field: "password", code: "validation.from_future" }]),
    );

    expect(result).toEqual([
      {
        field: "password",
        message: "One or more validation errors occurred.",
      },
    ]);
  });

  it("falls back to the response title when neither code resolves", () => {
    mockT.mockImplementation((key: string) => key);

    const result = tApiFieldErrors(
      validationFailed([{ field: "password", code: "validation.from_future" }]),
    );

    expect(result).toEqual([
      {
        field: "password",
        message: "One or more validation errors occurred.",
      },
    ]);
  });

  it("skips an entry that carries no code, since there is nothing to resolve", () => {
    mockT.mockImplementation(
      catalog({ "errors.validation.required": "This field is required." }),
    );

    const result = tApiFieldErrors(
      validationFailed([
        { field: "channelId" },
        { field: "displayName", code: "validation.required" },
      ]),
    );

    expect(result).toEqual([
      { field: "displayName", message: "This field is required." },
    ]);
  });

  it("reports an entry with no field under the empty name, so it is still rendered", () => {
    mockT.mockImplementation(
      catalog({ "errors.validation.required": "This field is required." }),
    );

    const result = tApiFieldErrors(
      validationFailed([{ code: "validation.required" }]),
    );

    expect(result).toEqual([{ field: "", message: "This field is required." }]);
  });

  it("ignores an errors value that is not an array", () => {
    const err = new ApiError({
      code: "validation.failed",
      params: { errors: "nope" },
      status: 400,
    });

    expect(tApiFieldErrors(err)).toEqual([]);
  });
});
