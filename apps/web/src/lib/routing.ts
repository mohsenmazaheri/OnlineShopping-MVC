import { isLocale } from "@/lib/i18n/config";
import type { StoreSettings } from "@/lib/store/settings";

/** Paths served without a locale prefix (assets, metadata routes, API proxy). */
const unlocalizedPrefixes = ["/api/", "/_next/", "/sw.js", "/manifest.webmanifest", "/robots.txt", "/sitemap.xml", "/icon", "/apple-icon"];

/**
 * Where to redirect a request whose path has no locale, or a locale this store does not offer; null to leave it.
 * Every page lives under /{locale}; "/" goes to the store's default locale. Redirects are temporary (307)
 * because the store's locales may change, and they never depend on the browser language — crawlers must
 * always land on the same URL (docs/SEO-MARKETING.md §2).
 */
export function localeRedirectTarget(
  pathname: string,
  search: string,
  store: Pick<StoreSettings, "defaultLocale" | "locales">,
): string | null {
  if (unlocalizedPrefixes.some((p) => pathname === p || pathname.startsWith(p))) return null;
  if (/\.[a-z0-9]+$/i.test(pathname)) return null; // static files from /public

  const [, firstSegment = ""] = pathname.split("/");
  if (isLocale(firstSegment)) {
    if (store.locales.includes(firstSegment)) return null;
    // A locale the platform knows but this store does not offer: same page in the default locale.
    const rest = pathname.slice(firstSegment.length + 1);
    return `/${store.defaultLocale}${rest}${search}`;
  }

  const rest = pathname === "/" ? "" : pathname;
  return `/${store.defaultLocale}${rest}${search}`;
}

/** Requests the proxy forwards to the .NET API (same origin for the browser: no CORS, API stays private). */
export function isApiPath(pathname: string): boolean {
  return pathname.startsWith("/api/v1/");
}
