/**
 * Locales the platform can render. A store enables a subset of these in the API (Store:Locales).
 * Adding a language = add it here, add src/lib/i18n/dictionaries/<code>.json, and allow it in the API's StoreOptions.
 */
export const supportedLocales = ["fa", "en", "hy", "de", "ar"] as const;

export type Locale = (typeof supportedLocales)[number];

const rtlLocales: ReadonlySet<Locale> = new Set<Locale>(["fa", "ar"]);

export function isLocale(value: string | undefined | null): value is Locale {
  return value != null && (supportedLocales as readonly string[]).includes(value);
}

export function directionOf(locale: Locale): "rtl" | "ltr" {
  return rtlLocales.has(locale) ? "rtl" : "ltr";
}
