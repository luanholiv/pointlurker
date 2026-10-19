/**
 * Turning the `field` on a `validation.failed` entry into something a user can read.
 *
 * The backend names the field by its wire name — `displayName`, or `values[0].labelCanonical` for a
 * collection contract. That is a property path, not a label, so it is developer-facing text and does
 * not belong in a sentence on its own. Each form owns a map from those names to the catalog keys it
 * already uses for its own inputs, which is what turns `displayName` into "Store name".
 */

/** Resolves a catalog key to its sentence. Narrower than i18next's `TFunction` on purpose. */
export type TranslateFn = (
  key: string,
  params?: Record<string, unknown>,
) => string;

/** Wire field names, indexes erased, mapped to the catalog key naming that input. */
export type FieldLabelMap = Record<string, string>;

/**
 * Supplies a label the catalog cannot: the store input profile labels its rows with strings that
 * come from the store's provider, so `values[0].value` is named by the row rather than by a key.
 * Returning `null` declines and hands the field back to the map.
 */
export type FieldLabelOverride = (field: string) => string | null;

const Index = /\[\d+\]/g;
const FirstIndex = /\[(\d+)\]/;

/**
 * The map key covering a field: every `[n]` becomes `[]`, so one entry covers every element of a
 * collection contract rather than the map needing a row per index.
 */
export function normalizeField(field: string): string {
  return field.replace(Index, "[]");
}

/** The element a collection field points at, or `null` when the field names a plain property. */
export function fieldIndex(field: string): number | null {
  const match = FirstIndex.exec(field);
  return match === null ? null : Number(match[1]);
}

/**
 * The label to show for a field: the override first, then the form's map, and finally the wire name
 * itself. That last step is deliberate — it is reachable only for a field no form maps, and naming
 * it badly still tells the user more than an anonymous "This field is required." does.
 */
export function resolveFieldLabel(
  field: string,
  map: FieldLabelMap,
  t: TranslateFn,
  override?: FieldLabelOverride,
): string {
  const overridden = override?.(field);
  if (overridden != null) {
    return overridden;
  }

  const key = map[normalizeField(field)];
  return key === undefined ? field : t(key);
}
