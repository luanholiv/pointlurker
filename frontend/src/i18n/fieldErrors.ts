import { ApiError } from "../lib/apiClient";
import { resolveCode, tError } from "./errorCatalog";
import {
  normalizeField,
  resolveFieldLabel,
  type FieldLabelMap,
  type FieldLabelOverride,
  type TranslateFn,
} from "./fieldLabels";

/**
 * One entry of a `validation.failed` body's `params.errors[]`: the rule that broke, named by its own
 * code, with its own interpolation params.
 */
export type FieldErrorEntry = {
  field?: string;
  code?: string;
  params?: Record<string, unknown>;
};

/** A per-field failure, localized and ready to render against the input it belongs to. */
export type ResolvedFieldError = {
  field: string;
  message: string;
};

/**
 * Every per-field failure an error reports, localized — or nothing when it reports none.
 *
 * `tApiError` answers with one string, so it can only ever show the first entry — one field-agnostic
 * sentence for a request that broke two fields. This returns them all, each still carrying its
 * `field`, so a form can attach each message to its own input.
 *
 * Recognised by shape rather than by the outer code, matching `tApiError`: all three backend
 * producers — request bodies, query filters and the framework Identity endpoints — emit the same
 * entry shape, so one branch resolves them.
 *
 * The `field` is read from the entry itself and not from its params. Both producers that go through
 * `FieldErrorProblem` repeat it inside params, but `IdentityErrorMapper` builds its entries by hand
 * and does not; the entry's own `field` is the one all three populate.
 */
export function tApiFieldErrors(err: unknown): ResolvedFieldError[] {
  if (!(err instanceof ApiError)) {
    return [];
  }

  const entries = err.params.errors;
  if (!Array.isArray(entries)) {
    return [];
  }

  return entries.flatMap((entry) =>
    resolveEntry(entry as FieldErrorEntry, err),
  );
}

/**
 * The key naming the input a field belongs to, or `null` when this form has no input for it.
 *
 * A function rather than a list of field names because a collection element cannot be resolved by
 * name alone: the store profile submits only the rows the user filled in, so `values[0]` is the
 * first *submitted* row rather than the first row on screen, and only the caller knows which.
 */
export type FieldAnchor = (field: string) => string | null;

export interface FieldErrorOptions {
  anchor: FieldAnchor;
  map: FieldLabelMap;
  t: TranslateFn;
  override?: FieldLabelOverride;
}

/**
 * Anchors the named fields to themselves, which is what a form with one input per field wants.
 * Names are matched normalized, so `values[].value` covers every element of a collection.
 */
export function anchorFields(...names: string[]): FieldAnchor {
  return (field) => (names.includes(normalizeField(field)) ? field : null);
}

/** No input for anything — every message is reported at form level, naming its own field. */
export const anchorNothing: FieldAnchor = () => null;

export interface FieldErrorSplit {
  /**
   * Messages per input, keyed by whatever the anchor named it. A list rather than one string
   * because a field can break several rules at once: Identity reports one entry per unmet password
   * requirement, so a single password routinely carries three or four.
   */
  byField: Record<string, string[]>;
  /** Messages for fields with no input on this form, each already naming the field it belongs to. */
  unattached: string[];
}

/**
 * Splits an error's per-field failures into the ones a form can attach to an input and the ones it
 * cannot, or `null` when the error reports none — which is the caller's cue to fall back to
 * `tApiError` for the non-`problem+json` shapes.
 *
 * A form with no input for a reported field is a real case rather than a mistake: the add-store
 * dialog submits a channel id and display name it took from a lookup, so neither is editable. Those
 * messages name their field in the sentence instead, which is the only way the user learns that two
 * rules broke rather than one.
 */
export function splitFieldErrors(
  err: unknown,
  { anchor, map, t, override }: FieldErrorOptions,
): FieldErrorSplit | null {
  const resolved = tApiFieldErrors(err);
  if (resolved.length === 0) {
    return null;
  }

  const byField: Record<string, string[]> = {};
  const unattached: string[] = [];

  for (const { field, message } of resolved) {
    const key = field === "" ? null : anchor(field);
    if (key !== null) {
      (byField[key] ??= []).push(message);
      continue;
    }
    unattached.push(
      field === ""
        ? message
        : t("validation.fieldMessage", {
            label: resolveFieldLabel(field, map, t, override),
            message,
          }),
    );
  }

  return { byField, unattached };
}

/**
 * An entry with no code names no rule, so there is nothing to resolve and nothing to say about it —
 * dropping it is better than rendering the wrapper's generic sentence once per malformed entry.
 * An unknown code does resolve, to the wrapper: a rule added by a future .NET version degrades to
 * "One or more validation errors occurred." rather than to nothing.
 */
function resolveEntry(
  entry: FieldErrorEntry,
  err: ApiError,
): ResolvedFieldError[] {
  if (typeof entry?.code !== "string") {
    return [];
  }

  const message =
    resolveCode(entry.code, entry.params) ??
    tError(err.code, err.params, err.title);

  return [{ field: entry.field ?? "", message }];
}
