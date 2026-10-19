/**
 * Locale-aware formatting helpers backed by the browser's Intl API.
 * Pass the active locale (e.g. from `i18n.language`) to each helper.
 */

export function formatDate(
  value: Date | string | number,
  locale: string,
  options?: Intl.DateTimeFormatOptions,
): string {
  const date = value instanceof Date ? value : new Date(value);
  return new Intl.DateTimeFormat(locale, options).format(date);
}

export function formatNumber(
  value: number,
  locale: string,
  options?: Intl.NumberFormatOptions,
): string {
  return new Intl.NumberFormat(locale, options).format(value);
}

/**
 * Joins values with the locale's own list separator and conjunction.
 * `["Steam ID", "Region"]` → `"Steam ID and Region"` in en-US, `"Steam ID e Region"` in pt-BR.
 * A hardcoded `", "` join leaks English punctuation into every locale.
 */
export function formatList(
  values: readonly string[],
  locale: string,
  options?: Intl.ListFormatOptions,
): string {
  return new Intl.ListFormat(locale, {
    style: "long",
    type: "conjunction",
    ...options,
  }).format(values);
}

export function formatRelativeTime(
  value: Date | string | number,
  locale: string,
  options?: Intl.RelativeTimeFormatOptions,
): string {
  const date = value instanceof Date ? value : new Date(value);
  const now = Date.now();
  const diffMs = date.getTime() - now;
  const diffSeconds = Math.round(diffMs / 1000);
  const diffMinutes = Math.round(diffSeconds / 60);
  const diffHours = Math.round(diffMinutes / 60);
  const diffDays = Math.round(diffHours / 24);

  const rtf = new Intl.RelativeTimeFormat(locale, {
    numeric: "auto",
    ...options,
  });

  if (Math.abs(diffDays) >= 1) return rtf.format(diffDays, "day");
  if (Math.abs(diffHours) >= 1) return rtf.format(diffHours, "hour");
  if (Math.abs(diffMinutes) >= 1) return rtf.format(diffMinutes, "minute");
  return rtf.format(diffSeconds, "second");
}
