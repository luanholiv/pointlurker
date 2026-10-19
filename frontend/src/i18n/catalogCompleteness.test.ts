/**
 * Fails when keys diverge between en-US.json and pt-BR.json.
 * This catches missing or extra keys in any locale.
 */
import { describe, it, expect } from "vitest";
import enUs from "./locales/en-US.json";
import ptBr from "./locales/pt-BR.json";

function collectLeafKeys(obj: unknown, prefix = ""): string[] {
  if (typeof obj !== "object" || obj === null) return [prefix];
  return Object.entries(obj as Record<string, unknown>).flatMap(
    ([key, value]) => collectLeafKeys(value, prefix ? `${prefix}.${key}` : key),
  );
}

describe("i18n catalog completeness", () => {
  const enKeys = new Set(collectLeafKeys(enUs));
  const ptKeys = new Set(collectLeafKeys(ptBr));

  it("pt-BR has no missing keys that are in en-US", () => {
    const missing = [...enKeys].filter((k) => !ptKeys.has(k));
    expect(
      missing,
      `Keys present in en-US but missing from pt-BR:\n${missing.join("\n")}`,
    ).toEqual([]);
  });

  it("pt-BR has no extra keys that are not in en-US", () => {
    const extra = [...ptKeys].filter((k) => !enKeys.has(k));
    expect(
      extra,
      `Keys present in pt-BR but not in en-US:\n${extra.join("\n")}`,
    ).toEqual([]);
  });
});
