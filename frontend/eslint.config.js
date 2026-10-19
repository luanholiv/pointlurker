import js from "@eslint/js";
import globals from "globals";
import reactHooks from "eslint-plugin-react-hooks";
import reactRefresh from "eslint-plugin-react-refresh";
import tseslint from "typescript-eslint";
import i18next from "eslint-plugin-i18next";
import { defineConfig, globalIgnores } from "eslint/config";

import noCommentHistory from "./eslint-rules/no-comment-history.js";
import noTrackerRefs from "./eslint-rules/no-tracker-refs.js";

export default defineConfig([
  globalIgnores(["dist", "coverage"]),
  {
    // Their own rules-only block, and a wider glob than everything below, because these rules read
    // comment tokens rather than AST nodes and so need no parser of their own. The `js` is what
    // brings this config file under them, and it carries prose like any other file. Widening the
    // block below instead would drag tseslint and react-refresh onto this file, where
    // `export default defineConfig([...])` trips only-export-components.
    files: ["**/*.{ts,tsx,js,mjs,cjs}"],
    plugins: {
      local: {
        rules: {
          "no-comment-history": noCommentHistory,
          "no-tracker-refs": noTrackerRefs,
        },
      },
    },
    rules: {
      "local/no-comment-history": "error",
      "local/no-tracker-refs": "error",
    },
  },
  {
    files: ["**/*.{ts,tsx}"],
    extends: [
      js.configs.recommended,
      tseslint.configs.recommended,
      reactHooks.configs.flat.recommended,
      reactRefresh.configs.vite,
    ],
    languageOptions: {
      ecmaVersion: 2020,
      globals: globals.browser,
    },
    rules: {
      // react-router-dom stops at 7.18.2 and is removed in react-router v8; it is
      // also where SNYK-JS-REACTROUTER-18313151 lives, with no fix on the 7.x line.
      // Reinstalling it would silently resurrect the advisory, and tsc alone would
      // not object.
      "no-restricted-imports": [
        "error",
        {
          paths: [
            {
              name: "react-router-dom",
              message: 'Import from "react-router" instead.',
            },
          ],
          patterns: [
            {
              group: ["react-router-dom/*"],
              message: 'Import from "react-router" instead.',
            },
          ],
        },
      ],
      // toLocale* with no explicit locale silently falls back to the *browser*
      // locale, which ignores the user's selected app locale — and an explicit
      // "en-US" hardcodes it, which is worse. Both shipped in the card
      // components. The src/i18n/formatters.ts helpers take the active locale
      // as an argument instead.
      "no-restricted-syntax": [
        "error",
        {
          selector:
            "CallExpression > MemberExpression[property.name=/^toLocale(String|DateString|TimeString)$/]",
          message:
            "Do not format dates or numbers with toLocale*. Use formatDate/formatNumber from src/i18n/formatters.ts and pass i18n.language.",
        },
        // `no-empty` (via js.configs.recommended) exempts any block holding a comment, which is
        // precisely how two `catch { // silently fail }` blocks cleared --max-warnings 0 and
        // shipped: the wishlist Edit button looked broken rather than failed, because the error
        // was discarded. A statement-less catch is never the intent, so it is banned outright.
        // A deliberate swallow still passes — it just has to say so with a statement, as
        // `authStorage.ts` does with `catch { return null; }`.
        {
          selector: "CatchClause > BlockStatement[body.length=0]",
          message:
            "Do not swallow an error. Report it (tApiError + a toast) or rethrow.",
        },
        // Ten catch blocks built their message as
        // `err instanceof Error ? err.message : t(key)`. The ternary is backwards: `err.message` is
        // almost always populated, so the catalog key was near-dead and what a user read was
        // whatever English the server or the transport had put in the message — on a pt-BR screen
        // too. `tApiError(err, key)` resolves the backend's code through the catalog and keeps the
        // key for everything else, so the branch is not needed anywhere.
        {
          selector:
            "BinaryExpression[operator='instanceof'][right.name='Error']",
          message:
            "Do not branch on `instanceof Error` to build a user-facing message — use tApiError(err, key).",
        },
        // Type is chosen by role, never by a number at the call site: a size picked per element
        // drifts, and the display face picked per element lands on data rather than headings. The
        // roles are the t-* classes in src/index.css, described in docs/DESIGN.md.
        {
          selector:
            "JSXAttribute[name.name='style'] Property[key.name=/^(fontSize|fontFamily)$/]",
          message:
            "Do not set fontSize or fontFamily inline. Use a typography role class (t-hero, t-heading, t-subheading, t-small, t-meta, t-eyebrow) — see docs/DESIGN.md.",
        },
      ],
    },
  },
  {
    // CLAUDE.md: "Frontend never hardcodes user-facing strings." Everything rendered to the
    // user comes from src/i18n/locales/. That was stated for a long time but not enforced,
    // so five components drifted back to English literals — see docs/I18N.md.
    files: [
      "src/features/**/*.tsx",
      "src/routes/**/*.tsx",
      "src/components/**/*.tsx",
    ],
    ignores: ["**/*.test.tsx"],
    plugins: { i18next },
    rules: {
      "i18next/no-literal-string": [
        "error",
        {
          // jsx-only (not jsx-text-only) so user-facing *attributes* are covered too:
          // placeholder, label, eyebrow, description, aria-label.
          mode: "jsx-only",
          message:
            "Move this string into src/i18n/locales and render it via t()",
          "jsx-attributes": {
            exclude: [
              // Plugin defaults, restated because supplying the key replaces them.
              "className",
              "styleName",
              "style",
              "type",
              "key",
              "id",
              "width",
              "height",
              // Structural / non-copy props used across this codebase.
              "data-testid",
              "testId",
              "tone",
              "variant",
              "to",
              "href",
              "htmlFor",
              "name",
              "autoComplete",
              "inputMode",
              "focusable",
              "role",
              "rel",
              "target",
              "path",
              "swipeDirection",
              // ARIA attributes whose values are tokens or element ids, not prose.
              // aria-label is deliberately absent — that one IS user-facing.
              "aria-current",
              "aria-describedby",
              "aria-labelledby",
              "aria-controls",
              "aria-live",
              // Form/control values and mode discriminators are data, not copy
              // (<option value="asc">, <WishlistEntryModal mode="edit">).
              "value",
              "mode",
            ],
          },
          "object-properties": {
            exclude: [
              // Plugin default, restated because supplying the key replaces it.
              "[A-Z_-]+",
              // Tab/section discriminators in local config arrays, paired with a
              // translated `label` alongside them.
              "id",
            ],
          },
          callees: {
            exclude: [
              // Plugin defaults, restated because supplying the key replaces them.
              "i18n(ext)?",
              "t",
              "require",
              "addEventListener",
              "removeEventListener",
              "postMessage",
              "getElementById",
              "dispatch",
              "commit",
              "includes",
              "indexOf",
              "endsWith",
              "startsWith",
              // Route paths, not copy.
              "navigate",
              // React state setters carry discriminator values, not copy.
              "set[A-Z].*",
              // src/i18n/formatters.ts helpers — their option objects are Intl config
              // (`hour: "2-digit"`), never user-facing text.
              "format[A-Z].*",
            ],
          },
          words: {
            exclude: [
              // Plugin defaults: ASCII punctuation/digits, SCREAMING_CASE, HTML entities, emoji.
              "[0-9!-/:-@[-`{-~]+",
              "[A-Z_-]+",
              /^\p{Emoji}+$/u,
              // Decorative glyphs and em-dash placeholders already in use: middot
              // separator, category marks, sort arrows, dismiss crosses, the enter-key
              // hint, and the empty-clock placeholder "—:——".
              "^[—·◆◇▲▼×✕↵:]+$",
              // snake_case API field names shown as technical labels, e.g. the
              // "channel_id ·" prefix in the store lookup result.
              "^[a-z0-9_]+\\s*·\\s*$",
            ],
          },
        },
      ],
    },
  },
]);
