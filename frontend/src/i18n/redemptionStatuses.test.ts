/**
 * Holds the catalog's status labels to the status list the app actually renders.
 *
 * The history page builds the key from a runtime value — `t(`redemption.status.${r.status}`)` — so
 * no source file spells those keys out, which is why `deadKeys.test.ts` allowlists the whole
 * prefix. That allowlist is only safe if something else can see them, and neither guard here could:
 * `catalogCompleteness.test.ts` compares the two locales to each other, so a status renamed in both
 * is wrong in both and passes.
 *
 * This is one half of the loop. The other half lives in the backend's
 * `Architecture.Tests/RedemptionStatusCatalogTests`, which holds the same catalog keys to the
 * `RedemptionStatus` enum. Splitting it that way is deliberate: CI path-filters the backend job to
 * `backend/**`, so a backend-side guard alone would never run on a pull request that touched only
 * this list — the direction most likely to drift. Enum → catalog → list is closed in both
 * directions, each half enforced by the stack whose files it watches.
 */
import { describe, it, expect } from "vitest";
import enUs from "./locales/en-US.json";
import ptBr from "./locales/pt-BR.json";
import { REDEMPTION_STATUSES } from "../features/redemptions/redemptionsApi";

const CATALOGS = { "en-US": enUs, "pt-BR": ptBr };

describe("redemption status labels", () => {
  it.each(Object.entries(CATALOGS))(
    "%s labels every status the app can render",
    (_locale, catalog) => {
      const labelled = Object.keys(catalog.redemption.status);

      // A typo in the JSON path would yield an empty list and compare nothing.
      expect(labelled.length).toBeGreaterThan(0);
      expect([...labelled].sort()).toEqual([...REDEMPTION_STATUSES].sort());
    },
  );
});
