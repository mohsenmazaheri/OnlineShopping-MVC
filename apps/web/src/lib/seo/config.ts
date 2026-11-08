import type { Metadata, MetadataRoute } from "next";
import type { Locale } from "@/lib/i18n/config";

type Env = Record<string, string | undefined>;

/** Public origin of the shop, e.g. https://shop.example.com (no trailing slash). */
export function siteUrl(env: Env = process.env): string {
  return (env.SITE_URL ?? "http://localhost:3000").replace(/\/+$/, "");
}

/**
 * Search engines may index this instance only when SEO_ALLOW_INDEXING is exactly "true".
 * Every other value (unset, test and staging instances) keeps the whole site out of search results.
 */
export function indexingAllowed(env: Env = process.env): boolean {
  return env.SEO_ALLOW_INDEXING === "true";
}

/** Paths that must never be crawled (see docs/SEO-MARKETING.md §6). */
// Pages live under /{locale}/…, so the rules need the locale wildcard ("/account" alone would never match).
export const privatePaths = ["/api/", "/*/account", "/*/cart", "/*/checkout", "/*/admin", "/*/search"];

export function buildRobots(env: Env = process.env): MetadataRoute.Robots {
  if (!indexingAllowed(env)) {
    return { rules: { userAgent: "*", disallow: "/" } };
  }
  return {
    rules: { userAgent: "*", allow: "/", disallow: privatePaths },
    sitemap: `${siteUrl(env)}/sitemap.xml`,
  };
}

/**
 * canonical + hreflang alternates for a page that exists in the given locales.
 * @param path path after the locale, starting with "/" ("/" for the home page)
 */
export function buildAlternates(
  path: string,
  locale: Locale,
  locales: readonly Locale[],
  fallbackLocale: Locale,
  env: Env = process.env,
): NonNullable<Metadata["alternates"]> {
  const base = siteUrl(env);
  const suffix = path === "/" ? "" : path;
  const url = (l: Locale) => `${base}/${l}${suffix}`;

  const languages: Record<string, string> = {};
  for (const l of locales) languages[l] = url(l);
  languages["x-default"] = url(locales.includes(fallbackLocale) ? fallbackLocale : locales[0] ?? locale);

  return { canonical: url(locale), languages };
}
