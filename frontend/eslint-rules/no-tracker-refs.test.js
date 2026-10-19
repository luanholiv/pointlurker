import { RuleTester } from "eslint";
import { describe, expect, it } from "vitest";

import noTrackerRefs, { classifyTrackerReference } from "./no-tracker-refs.js";

/**
 * Every fixture below is a string literal, so the rule — which matches this file too — sees no
 * comment here to flag. That is the property scanning comments rather than raw text buys, and it is
 * the only reason a suite of worked examples can exist at all.
 */
describe("classifyTrackerReference", () => {
  it.each([
    " Slate is #1e293b and white is #fff.",
    " Anchored at https://example.com/spec#L120.",
    " Every query it issues, so an extra call restates the default.",
    " RFC 7807, react-router 7.18.2 and SNYK-JS-REACTROUTER-18313151 are not citations.",
    " The same task 3 lines below repeats it.",
    " A Promise.all(2) call is not a citation either.",
    " user#1234 is a Discord tag shape, not a citation.",
  ])("passes %s", (text) => {
    expect(classifyTrackerReference(text)).toBeNull();
  });

  it.each([
    [" not object — see issue #110.", "named"],
    [" Regression test for issue #143.", "named"],
    [" Task 6.6 — Catalog completeness test.", "named"],
    [
      " the same regression task 6.9a removed from the redemption history.",
      "named",
    ],
    [" tracked as gh-1234", "named"],
    ["* a broad allowlist here recreates the #143 blind spot.", "bare"],
  ])("flags %s as %s", (text, expected) => {
    expect(classifyTrackerReference(text)).toBe(expected);
  });
});

const ruleTester = new RuleTester();

ruleTester.run("no-tracker-refs", noTrackerRefs, {
  valid: [
    'const tag = "luan#1234";',
    'const anchor = "https://example.com/spec#L120";',
    "// Slate is #1e293b; white is #fff.",
    "// Every query it issues, so an extra call restates the default.",
    "// The same task 3 lines below repeats it.",
  ],
  invalid: [
    {
      code: "// not object — see issue #110.",
      errors: [{ messageId: "named", line: 1 }],
    },
    {
      code: "/**\n * Regression test for issue #143.\n */",
      errors: [{ messageId: "named", line: 1 }],
    },
    {
      code: "/**\n * Task 6.6 — Catalog completeness test.\n */",
      errors: [{ messageId: "named", line: 1 }],
    },
    {
      code: "/**\n * a broad allowlist here recreates the #143 blind spot.\n */",
      errors: [{ messageId: "bare", line: 1 }],
    },
    {
      code: "const tag = 1; // the same regression task 6.9a removed from the history.",
      errors: [{ messageId: "named", line: 1 }],
    },
  ],
});
