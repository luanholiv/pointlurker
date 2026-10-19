/**
 * The attributes tying an input to the `FieldError` that reports its failures.
 *
 * Lives beside `FieldError.tsx` rather than in it because a module that exports a component may
 * export nothing else without breaking fast refresh. The `id` is the contract between the two:
 * pass the same one to both.
 *
 * Both attributes are dropped when the field is clean — pointing `aria-describedby` at an element
 * that is not rendered is a dangling reference, and marking a pristine field `aria-invalid="false"`
 * is noise.
 *
 * Note that `id` must not contain whitespace: `aria-describedby` is a space-separated list of ids,
 * so one built from a label like "Twitch Username" is read as two ids that match nothing.
 */
export function fieldErrorProps(id: string, messages?: readonly string[]) {
  const invalid = messages !== undefined && messages.length > 0;
  return {
    "aria-invalid": invalid || undefined,
    "aria-describedby": invalid ? id : undefined,
  };
}
