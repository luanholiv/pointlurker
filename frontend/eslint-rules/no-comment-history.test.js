import { RuleTester } from "eslint";
import { describe, expect, it } from "vitest";

import noCommentHistory, {
  classifyCommentHistory,
} from "./no-comment-history.js";

/**
 * Every fixture below is a string literal, so the rule — which matches this file too — sees no
 * comment here to flag. That is the property scanning comments rather than raw text buys, and it is
 * the only reason a suite of worked examples can exist at all.
 */
describe("classifyCommentHistory", () => {
  it.each([
    " The expression is used to build both halves of the query.",
    "*\n * the same expression is\n * used to build both halves.\n ",
    " The rows answer a query that is no longer the current one.",
    " Legacy callers reach the same endpoint.",
    " Sorting goes to the server, which orders the whole history before slicing.",
    " Asserting that the page hides a row would assert a rule it does not own.",
    " Pins the rule that a failed background tick keeps the rows it already has.",
  ])("passes %s", (text) => {
    expect(classifyCommentHistory(text)).toBeNull();
  });

  it.each([
    [" Sorting used to reorder the twenty rows already on screen.", "past"],
    [
      "*\n * The timeout sweep\n * used to sit behind the token checks.\n ",
      "past",
    ],
    [" Previously each repository call was its own commit.", "dated"],
    [
      " The bug this closes: the button looked inert rather than failed.",
      "defect",
    ],
    [" Regression test for the blank toast body.", "defect"],
  ])("flags %s as %s", (text, expected) => {
    expect(classifyCommentHistory(text)).toBe(expected);
  });
});

const ruleTester = new RuleTester();

ruleTester.run("no-comment-history", noCommentHistory, {
  valid: [
    'const doc = "this hook used to own the debounce";',
    "// The expression is used to build both halves of the query.",
    "/**\n * the same expression is\n * used to build both halves.\n */",
    "// The rows answer a query that is no longer the current one.",
    "// Sorting goes to the server, which orders the whole history before slicing.",
  ],
  invalid: [
    {
      code: "// Sorting used to reorder the twenty rows already on screen.",
      errors: [{ messageId: "past", line: 1 }],
    },
    {
      code: "/**\n * The timeout sweep\n * used to sit behind the token checks.\n */",
      errors: [{ messageId: "past", line: 1 }],
    },
    {
      code: "/**\n * Previously each repository call was its own commit.\n */",
      errors: [{ messageId: "dated", line: 1 }],
    },
    {
      code: "// The bug this closes: the button looked inert rather than failed.",
      errors: [{ messageId: "defect", line: 1 }],
    },
    {
      code: "const n = 1; // Regression test for the blank toast body.",
      errors: [{ messageId: "defect", line: 1 }],
    },
  ],
});
