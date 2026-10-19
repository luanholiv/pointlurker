import { useCallback, useState } from "react";
import {
  splitFieldErrors,
  type FieldErrorOptions,
  type FieldErrorSplit,
} from "../i18n/fieldErrors";

const EMPTY: FieldErrorSplit = { byField: {}, unattached: [] };

export interface UseFieldErrorsReturn extends FieldErrorSplit {
  /** Takes ownership of the error, or answers `false` when it reports no per-field failures. */
  setFromError: (err: unknown, options: FieldErrorOptions) => boolean;
  clear: () => void;
}

/**
 * Per-field failures from a rejected submission, held as form state.
 *
 * For the forms that catch their own rejection. A form whose hook already holds the error — the
 * store and wishlist mutations do — can derive the same split with `splitFieldErrors` instead of
 * keeping a second copy of it here.
 */
export function useFieldErrors(): UseFieldErrorsReturn {
  const [split, setSplit] = useState<FieldErrorSplit>(EMPTY);

  const clear = useCallback(() => setSplit(EMPTY), []);

  const setFromError = useCallback(
    (err: unknown, options: FieldErrorOptions) => {
      const next = splitFieldErrors(err, options);
      setSplit(next ?? EMPTY);
      return next !== null;
    },
    [],
  );

  return { ...split, setFromError, clear };
}
