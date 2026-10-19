/**
 * Holds the catalog's provider labels to the provider list the app renders.
 *
 * The badge builds its key from a runtime value — `t(providerLabelKey(store.provider))` — so no
 * source file spells those keys out, which is why `deadKeys.test.ts` allowlists the prefix.
 *
 * This is one half of the loop. The other half is the backend's
 * `Architecture.Tests/StoreProviderCatalogTests`, which holds the same keys to the `StoreProvider`
 * enum. CI path-filters the backend job to `backend/**`, so each half runs with the stack whose
 * files it watches.
 */
import { describe, it, expect } from "vitest";
import enUs from "./locales/en-US.json";
import ptBr from "./locales/pt-BR.json";
import { STORE_PROVIDERS } from "../features/stores/storeProviders";

const CATALOGS = { "en-US": enUs, "pt-BR": ptBr };

describe("store provider labels", () => {
  it.each(Object.entries(CATALOGS))(
    "%s labels every provider the app can render",
    (_locale, catalog) => {
      const labelled = Object.keys(catalog.providers);

      // A typo in the JSON path would yield an empty list and compare nothing.
      expect(labelled.length).toBeGreaterThan(0);
      expect([...labelled].sort()).toEqual([...STORE_PROVIDERS].sort());
    },
  );
});
