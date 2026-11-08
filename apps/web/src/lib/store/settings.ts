import { isLocale, supportedLocales, type Locale } from "@/lib/i18n/config";

/**
 * Store settings as the web app uses them. The API is the single source of truth (Store section of its
 * configuration); the same Docker image serves every shop because these are read at request time.
 */
export type StoreSettings = {
  name: string;
  defaultLocale: Locale;
  locales: Locale[];
  /** Sign-up methods offered (phone only when an SMS provider is configured). */
  signUp: { email: boolean; phone: boolean };
  /** Sign-in methods offered (SMS codes only when an SMS provider is configured). */
  signIn: { password: boolean; phoneCode: boolean };
};

export type StoreInfoLike = {
  name?: string | null;
  defaultLocale?: string | null;
  locales?: readonly string[] | null;
  signUp?: { email?: boolean | null; phone?: boolean | null } | null;
  signIn?: { password?: boolean | null; phoneCode?: boolean | null } | null;
};

/** Used when the API cannot be reached, so pages and redirects still work. */
export const fallbackStore: StoreSettings = {
  name: "Online Shop",
  defaultLocale: "fa",
  locales: [...supportedLocales],
  signUp: { email: true, phone: false },
  signIn: { password: true, phoneCode: false },
};

/** Validates API data: drops locales this web build cannot render and keeps the default among the locales. */
export function toStoreSettings(info: StoreInfoLike | null | undefined): StoreSettings {
  if (!info) return fallbackStore;

  const name = info.name?.trim() || fallbackStore.name;
  const signUp = { email: info.signUp?.email ?? true, phone: info.signUp?.phone ?? false };
  const signIn = { password: info.signIn?.password ?? true, phoneCode: info.signIn?.phoneCode ?? false };

  const locales = [...new Set((info.locales ?? []).filter(isLocale))];
  if (locales.length === 0) return { ...fallbackStore, name, signUp, signIn };

  const defaultLocale = isLocale(info.defaultLocale) && locales.includes(info.defaultLocale) ? info.defaultLocale : locales[0];
  return { name, defaultLocale, locales, signUp, signIn };
}
