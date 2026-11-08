import type { MetadataRoute } from "next";
import type { Locale } from "@/lib/i18n/config";
import { buildAlternates } from "./config";

/**
 * One sitemap entry per page and locale, each listing its hreflang alternates.
 * @param paths paths after the locale ("/" for the home page)
 */
export function buildSitemap(
  paths: readonly string[],
  locales: readonly Locale[],
  fallbackLocale: Locale,
  env: Record<string, string | undefined> = process.env,
): MetadataRoute.Sitemap {
  return paths.flatMap((path) =>
    locales.map((locale) => {
      const alternates = buildAlternates(path, locale, locales, fallbackLocale, env);
      return {
        url: alternates.canonical as string,
        alternates: { languages: alternates.languages as Record<string, string> },
      };
    }),
  );
}
