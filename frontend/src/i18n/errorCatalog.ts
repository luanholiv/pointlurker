import { ApiError } from "../lib/apiClient";
import type { FieldErrorEntry } from "./fieldErrors";
import i18n from "./index";

/**
 * Resolves `errors.<code>` in the active locale, or `null` when the catalog has no entry for it.
 * i18next answers an unknown key with the key itself, which is what makes the miss detectable.
 *
 * Exported for `fieldErrors.ts`, which resolves the same codes for every entry of a
 * `validation.failed` body rather than only the first.
 */
export function resolveCode(
  code: string,
  params?: Record<string, unknown>,
): string | null {
  const key = `errors.${code}`;
  const resolved = i18n.t(key, params as Record<string, string> | undefined);
  return resolved === key ? null : resolved;
}

/**
 * Resolves a backend error code to a localized string, falling back to `titleFallback` when the
 * catalog has no entry. An unmapped code is logged so it is easy to spot.
 */
export function tError(
  code: string,
  params?: Record<string, unknown>,
  titleFallback?: string,
): string {
  const resolved = resolveCode(code, params);
  if (resolved === null) {
    if (import.meta.env.DEV) {
      console.warn(
        `[tError] Unknown error code: "${code}" — falling back to title`,
      );
    }
    return titleFallback ?? code;
  }
  return resolved;
}

/**
 * The first per-field entry carrying a code, or `null` when this error does not report per-field
 * failures. Recognised by shape rather than by a specific outer code, so any endpoint reporting
 * field-level rules is covered.
 */
function firstFieldError(
  params: Record<string, unknown>,
): FieldErrorEntry | null {
  const entries = params.errors;
  if (!Array.isArray(entries) || entries.length === 0) {
    return null;
  }
  const first = entries[0] as FieldErrorEntry | null;
  return typeof first?.code === "string" ? first : null;
}

/**
 * Turns anything a rejected `apiClient` call can throw into a string fit to show a user.
 *
 * A backend `ProblemDetails` arrives as an `ApiError` and resolves through the catalog. Everything
 * else — `Error("Request failed with status 500")` for a non-`problem+json` response,
 * `Error("Session expired")` for a dead refresh, a raw `TypeError` from a dropped connection —
 * carries a developer-facing message, so the caller's `fallbackKey` — a catalog key, never a
 * literal — is what the user reads.
 *
 * A validation failure names the rule that broke in `params.errors[]` and leaves the outer code
 * generic, so the nested code is preferred when the catalog has it: "Password must be at least 8
 * characters." rather than "One or more validation errors occurred."
 */
export function tApiError(err: unknown, fallbackKey: string): string {
  if (!(err instanceof ApiError)) {
    return i18n.t(fallbackKey);
  }

  const fieldError = firstFieldError(err.params);
  if (fieldError?.code !== undefined) {
    const resolved = resolveCode(fieldError.code, fieldError.params);
    if (resolved !== null) {
      return resolved;
    }
  }

  return tError(err.code, err.params, err.title);
}
