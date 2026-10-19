/**
 * Guards the inverse of catalogCompleteness.test.ts.
 *
 * That test compares the two catalogs to each other, so it is blind when a key exists in
 * both locales but no component ever calls it. That is exactly how the card regression happened:
 * keys like `stores.viewItems` and `wishlist.noEntries` were authored, the components kept
 * their English literals, and nothing failed.
 */
import { describe, it, expect } from "vitest";
import enUs from "./locales/en-US.json";

/**
 * i18next resolves `t('key', { count })` to `key_one` / `key_other` at runtime, so the
 * suffixed leaves never appear in source. Match them against their base key instead.
 */
const PLURAL_SUFFIXES = ["_zero", "_one", "_two", "_few", "_many", "_other"];

/**
 * Keys selected by a runtime value rather than a literal, so no source file spells them out:
 * `errors.*` is reached through `tError(code, …)` with a backend error code,
 * `redemption.status.*` through a template literal over the API's status enum, and `providers.*`
 * through `providerLabelKey` over the API's provider slug. Matched by
 * prefix. Keep this list minimal — a broad allowlist here recreates the very blind spot this
 * file exists to close.
 *
 * An entry is only allowed to stay once a guard that CAN see the keys exists elsewhere; the
 * exemption is a delegation, not a pass. `errors.` is answered by the backend's
 * `Architecture.Tests/ErrorCodeCatalogKeyTests`, which binds every key to an `ErrorCodes`
 * constant, `redemption.status.` by `RedemptionStatusCatalogTests` plus this directory's
 * own `redemptionStatuses.test.ts`, and `providers.` by `StoreProviderCatalogTests` plus
 * `storeProviders.test.ts`. Do not add an entry without such a pair.
 */
const DYNAMIC_KEY_PREFIXES = ["errors.", "redemption.status.", "providers."];

function collectLeafKeys(obj: unknown, prefix = ""): string[] {
  if (typeof obj !== "object" || obj === null) return [prefix];
  return Object.entries(obj as Record<string, unknown>).flatMap(
    ([key, value]) => collectLeafKeys(value, prefix ? `${prefix}.${key}` : key),
  );
}

function stripPluralSuffix(key: string): string {
  const suffix = PLURAL_SUFFIXES.find((candidate) => key.endsWith(candidate));
  return suffix ? key.slice(0, -suffix.length) : key;
}

const sources = import.meta.glob("../**/*.{ts,tsx}", {
  query: "?raw",
  import: "default",
  eager: true,
}) as Record<string, string>;

// Test files are excluded on purpose: a key kept alive only by its own test is still dead.
const productionSource = Object.entries(sources)
  .filter(([path]) => !path.includes(".test."))
  .map(([, contents]) => contents)
  .join("\n");

function isReferenced(key: string): boolean {
  if (DYNAMIC_KEY_PREFIXES.some((prefix) => key.startsWith(prefix)))
    return true;
  const base = stripPluralSuffix(key);
  // Quote-delimited so `stores.remove` does not match `stores.removeError`.
  return (
    productionSource.includes(`'${base}'`) ||
    productionSource.includes(`"${base}"`) ||
    productionSource.includes(`\`${base}\``)
  );
}

describe("i18n dead keys", () => {
  it("every en-US catalog key is referenced by production source", () => {
    const unused = collectLeafKeys(enUs).filter((key) => !isReferenced(key));
    expect(
      unused,
      `Catalog keys no component references — wire them up or delete them from BOTH catalogs:\n${unused.join("\n")}`,
    ).toEqual([]);
  });
});
