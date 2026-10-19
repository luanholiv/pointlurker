/**
 * A comment states the rule the code holds; it does not recount the shape the code held before.
 * That account rots the way a tracker number does — nothing keeps it true once the code moves
 * again, and it does not survive a copy of the code — while `git blame` already reaches the commit
 * message, which is where it belongs.
 *
 * Built like `no-tracker-refs`, and for the same reasons: comments are not AST nodes, so this walks
 * `sourceCode.getAllComments()`, and every worked example lives in the test file as a string,
 * because a comment here demonstrating the shape would be the shape.
 *
 * The backend half is `CommentHistoryConventionTests` in Architecture.Tests. Two guards rather than
 * one because CI filters its jobs by path, so neither stack's gate runs on the other stack's
 * changes.
 */

/**
 * The phrase, minus the auxiliaries that turn it passive: "the expression is used to build both"
 * says nothing about any earlier version. The run between the auxiliary and the phrase is bounded
 * rather than a single space so the exclusion still holds when a block comment wraps between the
 * two words, where what sits in the gap is a newline and the leading `*`.
 */
const PAST_IMPLEMENTATION =
  /(?<!\b(?:is|are|was|were|be|been|being|get|gets|got|not|also|only|then)[\s/*]{1,16})\bused to\b/i;

/** A statement dated against an earlier version rather than made about the code as it stands. */
const EARLIER_VERSION = /\bpreviously\b/i;

/**
 * A defect named rather than described. A test that says which failure it pins is useful; one that
 * says a failure once existed is dated the moment it is fixed.
 */
const NAMED_DEFECT =
  /\bthe (?:bug|defect|regression|problem) (?:this|that)\b|\bregression test for\b/i;

/**
 * The message id this comment text earns, or `null` when it is clean. Exported so the patterns can
 * be exercised directly, without standing a linter up around them.
 *
 * @param {string} text
 * @returns {"past" | "dated" | "defect" | null}
 */
export function classifyCommentHistory(text) {
  if (PAST_IMPLEMENTATION.test(text)) {
    return "past";
  }

  if (EARLIER_VERSION.test(text)) {
    return "dated";
  }

  return NAMED_DEFECT.test(text) ? "defect" : null;
}

/** @type {import("eslint").Rule.RuleModule} */
const noCommentHistory = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Disallow comments that recount what the code did before; state the rule it holds now instead.",
    },
    schema: [],
    messages: {
      past: "This comment recounts what the code did before. State the rule it holds now — the shape it held before belongs in the commit message, which is where `git blame` already leads.",
      dated:
        "This comment dates a statement against an earlier version. State the rule the code holds now — the comparison belongs in the commit message, which is where `git blame` already leads.",
      defect:
        "This comment names the defect a change closed. Say which rule the code guards instead — a test named for a failure is dated the moment it is fixed.",
    },
  },
  create(context) {
    return {
      "Program:exit"() {
        for (const comment of context.sourceCode.getAllComments()) {
          const messageId = classifyCommentHistory(comment.value);

          if (messageId) {
            context.report({ loc: comment.loc, messageId });
          }
        }
      },
    };
  },
};

export default noCommentHistory;
