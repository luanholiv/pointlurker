import { describe, it, expect } from "vitest";
import {
  fieldIndex,
  normalizeField,
  resolveFieldLabel,
  type FieldLabelMap,
} from "./fieldLabels";

const t = (key: string) => `translated:${key}`;

describe("normalizeField", () => {
  it("leaves a plain property alone", () => {
    expect(normalizeField("displayName")).toBe("displayName");
  });

  it("erases the index of a collection element", () => {
    expect(normalizeField("values[0].value")).toBe("values[].value");
  });

  it("erases every index, so a nested collection maps to one entry", () => {
    expect(normalizeField("groups[2].values[11].value")).toBe(
      "groups[].values[].value",
    );
  });

  it("is a no-op on a field that is already normalized", () => {
    expect(normalizeField("values[].value")).toBe("values[].value");
  });
});

describe("fieldIndex", () => {
  it("reads the index of a collection element", () => {
    expect(fieldIndex("values[0].value")).toBe(0);
    expect(fieldIndex("values[7].labelCanonical")).toBe(7);
  });

  it("returns null for a plain property", () => {
    expect(fieldIndex("displayName")).toBeNull();
  });

  it("returns the first index when several are present", () => {
    expect(fieldIndex("groups[2].values[11].value")).toBe(2);
  });
});

describe("resolveFieldLabel", () => {
  const map: FieldLabelMap = {
    displayName: "stores.displayName",
    "values[].value": "stores.profile.valueLabel",
  };

  it("translates the catalog key the map names", () => {
    expect(resolveFieldLabel("displayName", map, t)).toBe(
      "translated:stores.displayName",
    );
  });

  it("maps every element of a collection through one entry", () => {
    expect(resolveFieldLabel("values[3].value", map, t)).toBe(
      "translated:stores.profile.valueLabel",
    );
  });

  it("prefers an override, which is how a server-supplied row label wins", () => {
    const override = (field: string) =>
      field === "values[1].value" ? "Twitch username" : null;

    expect(resolveFieldLabel("values[1].value", map, t, override)).toBe(
      "Twitch username",
    );
  });

  it("falls back to the map when the override declines", () => {
    const override = () => null;

    expect(resolveFieldLabel("displayName", map, t, override)).toBe(
      "translated:stores.displayName",
    );
  });

  // The last resort is a wire name, which is developer-facing text. It is reachable only for a
  // field no form maps, so naming it beats leaving the user with an anonymous sentence.
  it("falls back to the raw field name when nothing maps it", () => {
    expect(resolveFieldLabel("channelId", map, t)).toBe("channelId");
  });

  it("keeps the index in the raw fallback, so one element of a collection is identifiable", () => {
    expect(resolveFieldLabel("values[2].labelCanonical", map, t)).toBe(
      "values[2].labelCanonical",
    );
  });
});
