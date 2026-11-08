import type { Locale } from "@/lib/i18n/config";

/**
 * Where to send the user after signing in. Only a path inside this site and this locale is accepted;
 * anything else ("//evil.com", "https://evil.com", "/\evil.com", other locales) falls back to the home page.
 * Prevents open redirects through ?returnUrl=.
 */
export function safeReturnPath(value: string | null | undefined, locale: Locale): string {
  const home = `/${locale}`;
  if (!value) return home;
  if (!value.startsWith("/") || value.startsWith("//") || value.includes("\\")) return home;
  if (value !== home && !value.startsWith(`${home}/`) && !value.startsWith(`${home}?`)) return home;
  if (value.startsWith(`${home}/account/login`) || value.startsWith(`${home}/account/register`)) return home;
  return value;
}
