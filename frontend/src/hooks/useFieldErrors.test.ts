import { act, renderHook } from "@testing-library/react";
import { anchorFields, anchorNothing } from "../i18n/fieldErrors";
import { ApiError } from "../lib/apiClient";
import { useFieldErrors } from "./useFieldErrors";

/**
 * Stands in for i18next: the label keys the maps name resolve to a sentence, and
 * `validation.fieldMessage` composes a label with a message the way the catalog does.
 */
const t = (key: string, params?: Record<string, unknown>) => {
  if (key === "validation.fieldMessage") {
    return `${String(params?.label)}: ${String(params?.message)}`;
  }
  const labels: Record<string, string> = {
    "stores.channelId": "Channel",
    "stores.displayName": "Store name",
    "settings.jwtTokenLabel": "JWT Token",
  };
  return labels[key] ?? key;
};

function validationFailed(errors: unknown[]) {
  return new ApiError({
    code: "validation.failed",
    params: { errors },
    status: 400,
    title: "One or more validation errors occurred.",
  });
}

const storeLabels = {
  channelId: "stores.channelId",
  displayName: "stores.displayName",
};

describe("useFieldErrors", () => {
  it("starts empty", () => {
    const { result } = renderHook(() => useFieldErrors());

    expect(result.current.byField).toEqual({});
    expect(result.current.unattached).toEqual([]);
  });

  it("declines an error that carries no per-field entries", () => {
    const { result } = renderHook(() => useFieldErrors());

    let taken = true;
    act(() => {
      taken = result.current.setFromError(new Error("boom"), {
        anchor: anchorFields("displayName"),
        map: storeLabels,
        t,
      });
    });

    expect(taken).toBe(false);
    expect(result.current.byField).toEqual({});
    expect(result.current.unattached).toEqual([]);
  });

  it("attaches an entry to the input that owns it", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "displayName", code: "validation.required" },
        ]),
        { anchor: anchorFields("displayName"), map: storeLabels, t },
      );
    });

    expect(result.current.byField).toEqual({
      displayName: ["This field is required."],
    });
    expect(result.current.unattached).toEqual([]);
  });

  // The point of the fix: two broken fields must produce two messages, each on its own input.
  it("attaches every entry when the form anchors them all", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "channelId", code: "validation.required" },
          { field: "displayName", code: "validation.required" },
        ]),
        {
          anchor: anchorFields("channelId", "displayName"),
          map: storeLabels,
          t,
        },
      );
    });

    expect(result.current.byField).toEqual({
      channelId: ["This field is required."],
      displayName: ["This field is required."],
    });
  });

  // Identity reports one entry per unmet password requirement, so several rules routinely land on
  // one field. Keeping only the last would hide the rest behind a fix-one-at-a-time loop.
  it("keeps every rule that broke on the same field", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "password", code: "validation.password_requires_digit" },
          { field: "password", code: "validation.password_requires_upper" },
        ]),
        { anchor: anchorFields("password"), map: {}, t },
      );
    });

    expect(result.current.byField).toEqual({
      password: [
        "Password must contain at least one digit.",
        "Password must contain at least one uppercase letter.",
      ],
    });
  });

  // AddStoreDialog is this case: neither validated field has an input, so both must still be shown
  // at form level, and each must name itself or the user learns nothing.
  it("names a field it cannot attach", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "channelId", code: "validation.required" },
          { field: "displayName", code: "validation.required" },
        ]),
        { anchor: anchorNothing, map: storeLabels, t },
      );
    });

    expect(result.current.byField).toEqual({});
    expect(result.current.unattached).toEqual([
      "Channel: This field is required.",
      "Store name: This field is required.",
    ]);
  });

  it("splits a response that breaks one anchored and one unanchored field", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "channelId", code: "validation.required" },
          { field: "displayName", code: "validation.required" },
        ]),
        { anchor: anchorFields("displayName"), map: storeLabels, t },
      );
    });

    expect(result.current.byField).toEqual({
      displayName: ["This field is required."],
    });
    expect(result.current.unattached).toEqual([
      "Channel: This field is required.",
    ]);
  });

  it("anchors a collection element through its normalized field", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "values[2].value", code: "validation.required" },
        ]),
        { anchor: anchorFields("values[].value"), map: {}, t },
      );
    });

    expect(result.current.byField).toEqual({
      "values[2].value": ["This field is required."],
    });
  });

  // A collection contract reports the index of the element as *submitted*, and a form that sends
  // only the rows the user filled in cannot always map that back to a row on screen. When it
  // cannot, the message must still be shown rather than silently attached to nothing.
  it("reports a collection element the form cannot place", () => {
    const { result } = renderHook(() => useFieldErrors());
    const rows = ["Twitch username"];

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "values[4].value", code: "validation.required" },
        ]),
        {
          anchor: (field) => rows[Number(/\[(\d+)\]/.exec(field)?.[1])] ?? null,
          map: {},
          t,
          override: () => "Row 5",
        },
      );
    });

    expect(result.current.byField).toEqual({});
    expect(result.current.unattached).toEqual([
      "Row 5: This field is required.",
    ]);
  });

  it("anchors a collection element under the key the form chooses for it", () => {
    const { result } = renderHook(() => useFieldErrors());
    const rows = ["Twitch username", "Discord tag"];

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "values[1].value", code: "validation.required" },
        ]),
        {
          anchor: (field) => rows[Number(/\[(\d+)\]/.exec(field)?.[1])] ?? null,
          map: {},
          t,
        },
      );
    });

    expect(result.current.byField).toEqual({
      "Discord tag": ["This field is required."],
    });
  });

  it("labels an unanchored collection element from the override", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "values[1].value", code: "validation.required" },
        ]),
        {
          anchor: anchorNothing,
          map: {},
          t,
          override: (field) =>
            field === "values[1].value" ? "Twitch username" : null,
        },
      );
    });

    expect(result.current.unattached).toEqual([
      "Twitch username: This field is required.",
    ]);
  });

  it("renders a message with no field on its own, with nothing to label it with", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([{ code: "validation.required" }]),
        { anchor: anchorNothing, map: storeLabels, t },
      );
    });

    expect(result.current.unattached).toEqual(["This field is required."]);
  });

  it("clears what a previous submission reported", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "displayName", code: "validation.required" },
          { field: "channelId", code: "validation.required" },
        ]),
        { anchor: anchorFields("displayName"), map: storeLabels, t },
      );
    });
    act(() => {
      result.current.clear();
    });

    expect(result.current.byField).toEqual({});
    expect(result.current.unattached).toEqual([]);
  });

  it("replaces the previous submission's errors rather than accumulating them", () => {
    const { result } = renderHook(() => useFieldErrors());

    act(() => {
      result.current.setFromError(
        validationFailed([{ field: "channelId", code: "validation.required" }]),
        {
          anchor: anchorFields("channelId", "displayName"),
          map: storeLabels,
          t,
        },
      );
    });
    act(() => {
      result.current.setFromError(
        validationFailed([
          { field: "displayName", code: "validation.required" },
        ]),
        {
          anchor: anchorFields("channelId", "displayName"),
          map: storeLabels,
          t,
        },
      );
    });

    expect(result.current.byField).toEqual({
      displayName: ["This field is required."],
    });
  });
});
