/**
 * Catalog placeholder parity.
 *
 * `catalogCompleteness.test.ts` proves the two catalogs hold the same keys, but it compares key
 * sets and never looks at the sentences. Two messages under one key can interpolate different
 * placeholders and it passes — so a `{{max}}` present in en-US and dropped from pt-BR ships a
 * pt-BR sentence with the number silently missing from it, and nothing fails.
 *
 * This closes that direction: whatever a message interpolates, its counterpart must interpolate
 * too. The check covers the whole catalog rather than `errors.*` alone, because the failure has
 * nothing to do with error codes — `stores.watchedLabel` is "{{filtered}} / {{total}}" and would
 * read just as wrong with one of them gone.
 *
 * The complementary direction — a placeholder no backend param supplies — cannot be seen from
 * here, since it needs the backend's error contract. `Architecture.Tests/ErrorCodePlaceholderTests`
 * owns it.
 */
import { describe, it, expect } from "vitest";
import enUs from "./locales/en-US.json";
import ptBr from "./locales/pt-BR.json";

function collectMessages(value: unknown, prefix = ""): Array<[string, string]> {
  if (typeof value === "string") return [[prefix, value]];
  if (typeof value !== "object" || value === null) return [];

  return Object.entries(value as Record<string, unknown>).flatMap(
    ([key, child]) => collectMessages(child, prefix ? `${prefix}.${key}` : key),
  );
}

/** The i18next interpolations in a message, deduped and ordered so two sets compare directly. */
function placeholdersIn(message: string): string[] {
  const names = [...message.matchAll(/\{\{\s*([^}\s]+?)\s*\}\}/g)].map(
    (match) => match[1],
  );
  return [...new Set(names)].sort();
}

describe("i18n placeholder parity", () => {
  const enMessages = new Map(collectMessages(enUs));
  const ptMessages = new Map(collectMessages(ptBr));

  const placeholders = (messages: Map<string, string>, key: string) =>
    placeholdersIn(messages.get(key) ?? "");

  // Keys missing from a locale are catalogCompleteness.test.ts's finding, not this one's.
  const shared = [...enMessages.keys()].filter((key) => ptMessages.has(key));

  const interpolating = shared.filter(
    (key) =>
      placeholders(enMessages, key).length > 0 ||
      placeholders(ptMessages, key).length > 0,
  );

  it("finds interpolating messages to compare", () => {
    expect(
      interpolating.length,
      "No message in either catalog was found to interpolate anything, which cannot be right. " +
        "The placeholder pattern has drifted from the catalogs and this suite is passing " +
        "without comparing anything.",
    ).toBeGreaterThan(0);
  });

  it("interpolates the same placeholders in both locales", () => {
    const mismatched = interpolating
      .filter(
        (key) =>
          placeholders(enMessages, key).join(",") !==
          placeholders(ptMessages, key).join(","),
      )
      .map(
        (key) =>
          `${key}: en-US has [${placeholders(enMessages, key).join(", ")}], ` +
          `pt-BR has [${placeholders(ptMessages, key).join(", ")}]`,
      );

    expect(
      mismatched,
      `Messages whose locales interpolate different placeholders:\n${mismatched.join("\n")}`,
    ).toEqual([]);
  });
});
