/**
 * A comment states the mechanism; it does not name the tracker item the mechanism came from. The
 * number goes stale as soon as the tracker moves, and it belongs in the commit message or the pull
 * request body — the history, which `git blame` already reaches.
 *
 * Comments are not AST nodes, so `no-restricted-syntax` cannot reach them and the core
 * `no-warning-comments` rule only takes literal terms rather than a pattern. This walks
 * `sourceCode.getAllComments()` instead.
 *
 * Reading comments rather than raw text is also what keeps the Discord-tag fixtures out of it:
 * those are string literals, and a literal is not a comment. The same property is why the worked
 * examples live in this rule's test file as strings — a comment here demonstrating a citation would
 * be a citation.
 *
 * The backend half of the same rule is `CommentConventionTests` in Architecture.Tests. Two guards
 * rather than one because CI filters its jobs by path, so neither stack's gate runs on the other
 * stack's changes.
 */

/**
 * A tracker word joined to a number. With an explicit marker between them any digits count; without
 * one the number has to be dotted or at least two digits, which is what keeps prose out of it — a
 * sentence counting lines below is not a citation, while a dotted plan number is. A period is
 * deliberately not a marker, so a method group on a promise-like type cannot be read as one.
 */
const NAMED_REFERENCE =
  /\b(?:issues?|tasks?|tickets?|bugs?|stor(?:y|ies)|epics?|prs?|gh)\b(?:[ \t]*[#:-][ \t]*[0-9]|[ \t]+(?:[0-9]+(?:\.[0-9]+)+[a-z]?|[0-9]{2,}))/i;

/**
 * A hash and at least two digits, with a word character on neither side, for when the number is all
 * that is left of the citation. The boundaries are what exclude a hex colour, a Discord tag and a
 * markdown anchor — none of which reach a comment scan anyway, but the pattern should be honest on
 * its own.
 */
const BARE_NUMBER = /(?:^|[^0-9A-Za-z_])#[0-9]{2,}(?![0-9A-Za-z_-])/;

/**
 * The message id this comment text earns, or `null` when it is clean. Exported so the patterns can
 * be exercised directly, without standing a linter up around them.
 *
 * @param {string} text
 * @returns {"named" | "bare" | null}
 */
export function classifyTrackerReference(text) {
  if (NAMED_REFERENCE.test(text)) {
    return "named";
  }

  return BARE_NUMBER.test(text) ? "bare" : null;
}

/** @type {import("eslint").Rule.RuleModule} */
const noTrackerRefs = {
  meta: {
    type: "problem",
    docs: {
      description:
        "Disallow issue, pull request, task and ticket numbers in comments; state the mechanism instead.",
    },
    schema: [],
    messages: {
      named:
        "This comment names a tracker item. Say what broke instead — the reference belongs in the commit message or the pull request body, which is where `git blame` already leads.",
      bare: "This comment carries a bare tracker number. Say what broke instead — the reference belongs in the commit message or the pull request body, which is where `git blame` already leads.",
    },
  },
  create(context) {
    return {
      "Program:exit"() {
        for (const comment of context.sourceCode.getAllComments()) {
          const messageId = classifyTrackerReference(comment.value);

          if (messageId) {
            context.report({ loc: comment.loc, messageId });
          }
        }
      },
    };
  },
};

export default noTrackerRefs;
