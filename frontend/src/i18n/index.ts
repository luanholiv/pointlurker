import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import enUs from "./locales/en-US.json";
import ptBr from "./locales/pt-BR.json";

/** BCP-47 tags that match the backend SupportedLocales allowlist. */
export const SUPPORTED_LOCALES = ["en-US", "pt-BR"] as const;
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number];
export const DEFAULT_LOCALE: SupportedLocale = "en-US";

/**
 * Resolves the initial locale before a user is authenticated.
 * Uses an exact-match against the browser's preferred language so that
 * `pt-PT` does NOT match `pt-BR` — only exact BCP-47 tags resolve.
 */
export function resolveInitialLocale(
  languages?: readonly string[],
): SupportedLocale {
  const candidates = languages ?? navigator.languages ?? [navigator.language];
  for (const lang of candidates) {
    if ((SUPPORTED_LOCALES as readonly string[]).includes(lang)) {
      return lang as SupportedLocale;
    }
  }
  return DEFAULT_LOCALE;
}

i18n.use(initReactI18next).init({
  lng: resolveInitialLocale(),
  fallbackLng: DEFAULT_LOCALE,
  resources: {
    "en-US": { translation: enUs },
    "pt-BR": { translation: ptBr },
  },
  interpolation: {
    escapeValue: false, // React already escapes output
  },
});

export default i18n;
