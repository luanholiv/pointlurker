# Design system

The frontend's visual language: a small set of type roles, colour tokens and radii, all defined in
`frontend/src/index.css`. This document is the contract; the stylesheet is the implementation.

Three faces, each with one job:

| Face | Token | Job |
|---|---|---|
| Bricolage Grotesque | `--f-display` | Headings and big numbers. Never data. |
| Inter Tight | `--f-body` | Everything people read: prose, values, controls. |
| JetBrains Mono | `--f-mono` | Labels, and technical or tabular values. |

## Type roles

Every piece of text has exactly one role. Pick the role by what the text *is*, not by how big it
should look.

| Role | Class | Face · size | Element | Use for |
|---|---|---|---|---|
| Title | `PageHead` (`.page-head h1`) | display · 52 | `h1` | The page title. Once per page. |
| Hero | `.t-hero` | display · 44 | `h1`, `div` | The one focal figure of a screen without a page head: the sign-in card title, a KPI value, a countdown. |
| Heading | `.t-heading` | display · 22 | `h2` | A panel, modal or section. Names its subject. |
| Subheading | `.t-subheading` | display · 18 | `h3` | A card inside a section: a store, an item. |
| Body | *(none — the default)* | body · 14 | — | Primary text and values: an email, an item count, input text. |
| Small | `.t-small` | body · 13 | — | Descriptions, help text, status and error messages. |
| Meta | `.t-meta` | mono · 12 | — | Timestamps, ids, costs, counters, keys and codes. |
| Label | `.t-eyebrow` | mono caps · 10.5 | `label`, `div` | Field labels, keys in key/value rows, column headers, kickers. |

One size sits outside the roles because it is decoration, not text: the item glyph
(`.item-media-glyph`, 56). `.kpi .val` is the Hero role applied by its component. Controls
(`.btn`, `.chip`, `.pill`, `.tab`, `.nav-*`) take their sizes from the same tokens and need no
class at the call site.

Each size is a token on `:root` — `--fs-title`, `--fs-heading`, `--fs-subheading`, `--fs-body`,
`--fs-small`, `--fs-meta`, `--fs-label`, `--fs-hero`. CSS refers to tokens, never to pixels.

Colour is independent of role and composes with it: `.t-muted` (ink-3), `.t-dim` (ink-4), `.t-ok`,
`.t-bad`. A status line is `className="t-small t-bad"`.

## Rules

1. **The display face is for headings.** Data — an email, a name inside a sentence, an id — is Body
   or Meta, however important it is. Importance is position and weight of the panel, not the face.
2. **One heading per panel, and it names the subject.** "StreamElements", not "Token"; "Change
   password", not "Credentials". If a panel needs two levels of title, it is two panels.
3. **No label above a heading.** A Label (`t-eyebrow`) above a Heading competes with it and, being
   smaller, inverts the hierarchy when it carries the more specific word. A kicker is allowed only
   on the page head, where it gives context to a title that already names the subject.
4. **A label never repeats its heading.** Under "StreamElements" the field is "JWT Token", not
   "StreamElements JWT Token".
5. **Step down one role at a time inside a container.** Heading → Body/Small → Meta/Label. A card
   is Subheading → Body → Meta.
6. **Headings are real heading elements**, styled by class. Screen readers navigate by them; the
   classes reset their margins.
7. **No inline `fontSize` or `fontFamily`.** Lint rejects both (`no-restricted-syntax` in
   `frontend/eslint.config.js`). A case no role fits is a change to this document and to
   `index.css`, not a one-off number.

### Worked example: Settings

| Before | After |
|---|---|
| Label `IDENTITY` over the email in the 22px display face | Heading `Identity`; rows `E-MAIL → address` (Body), `USER ID → uuid` (Meta) |
| Label `STREAMELEMENTS JWT` over heading `Token`, field `JWT TOKEN` | Heading `StreamElements`, field `JWT TOKEN` |
| Label `GAUPOINTS SESSION` over heading `Session cookie` | Heading `GauPoints`, help text in Small |
| Label `DANGER ZONE` over heading `Sessions` | Heading `Sessions` in a `.panel-danger` |

## Colour

OKLCH tokens, redefined per theme under `[data-theme="dark"]` and `[data-theme="light"]`. Tailwind
reads them through `@theme inline`, so `bg-card` and `text-ink-3` work as utilities.

| Token | Use |
|---|---|
| `--bg`, `--bg-elev` | Page and sticky chrome |
| `--panel`, `--card`, `--card-2` | Surfaces, in nesting order |
| `--line`, `--line-soft` | Borders and dividers |
| `--ink` … `--ink-4` | Text, from primary to disabled |
| `--accent` (+ `-soft`, `-line`, `-ink`) | The one interactive colour: the gold of the app icon's coin. Dark sets it bright with dark `-ink`; light deepens it to amber with near-white `-ink` so both clear 4.5:1. Hue in `--accent-h`. |
| `--ok`, `--warn`, `--bad` (+ `-soft`) | Status, always paired with a word, never colour alone |

## Shape

Radii are deliberately small: `--r-1` 2px, `--r-2` 3px, `--r-3` 4px, `--r-4` 6px (panels and
modals). Panels and modals pad 22px; stacks use `gap` on `.vstack`/`.hstack`.

## Motion

Motion confirms an action or shows where something came from. It never decorates. All of it is CSS
on the primitives' own state attributes plus the View Transitions API; there is no animation
library.

| Token | Value | Use |
|---|---|---|
| `--dur-1` | 120ms | Hover, press release, and every exit |
| `--dur-2` | 200ms | Dialog and toast entry |
| `--dur-3` | 300ms | Drawer, route change, nav marker, card and empty-state entrance |
| `--dur-4` | 520ms | Theme reveal, wishlist burst |
| `--ease-out` | `cubic-bezier(.23, 1, .32, 1)` | Anything entering or answering input |
| `--ease-in-out` | `cubic-bezier(.77, 0, .175, 1)` | The theme reveal, which moves across the screen |
| `--ease-spring` | `linear(…)`, about 16% overshoot | Toast snap-back, wishlist burst |
| `--rise`, `--scale-from` | `8px`, `.97` | The travel of every entrance |

1. **Exits are faster than entries** and use `ease-in`. Closing something must never feel like
   waiting.
2. **Nothing scales from zero.** Entrances start at `--scale-from` with opacity 0.
3. **Transition named properties**, never `all`.
4. **Radix primitives animate on `[data-state="open"|"closed"]`.** Radix keeps a closing element
   mounted until its `animationend`, so an exit is a keyframe on `closed`, nothing more. The modal
   is centred with `translate`, which leaves `transform` free for the animation.
5. **Route changes are View Transitions.** `NavList` runs `navigate` through `withViewTransition`
   (`src/lib/viewTransition.ts`), and `BrowserRouter` has `useTransitions={false}` so the new page
   commits before the browser snapshots it. Only `.page` (crossfade and rise) and `.nav-ind`
   (slides to the new item) are named; the rest of the shell holds still.
6. **Lists stagger by at most five steps** (`.stagger`, 40ms apart). A list is never held back
   waiting for its tail.
7. **Reduced motion keeps fades and drops travel.** The `prefers-reduced-motion` block zeroes
   `--rise` and `--scale-from`, turns off the press scale, the burst, the shimmer and the pulse,
   and `withViewTransition` skips the transition entirely.

## Empty states

Every empty list renders `EmptyState`: an icon tile, a Subheading that names what is missing, one
Small sentence on what fills it, and at most one action. Its `tone` says which kind of empty it
is, and the kinds never share copy:

| Tone | Means | Tile | Action |
|---|---|---|---|
| `first-run` | Nothing exists yet | Accent | The one that creates the first thing |
| `filtered` | Things exist, the search or filters hide them | Neutral | Clear the filters |
| `idle` | Nothing is missing that the reader can supply | Neutral | None |

While a card grid loads it shows `SkeletonGrid` in the same grid, never a loading sentence in an
empty box, so nothing moves when the data arrives.

## Reviewing a change

`frontend/tests/ui/design-screenshots.spec.ts` captures Stores, Wishlist, Redemptions and each
Settings tab in both themes, attached to the Playwright HTML report. To write them to a folder:

```sh
cd frontend
DESIGN_SCREENSHOT_DIR=test-results/design-shots npx playwright test --project=ui tests/ui/design-screenshots.spec.ts
```
