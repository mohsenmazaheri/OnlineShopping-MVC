import type { Metadata } from "next";
import { isLocale, type Locale } from "@/lib/i18n/config";

/** Account pages are private: never indexed, never followed (docs/SEO-MARKETING.md §6). */
export function accountMetadata(title: string): Metadata {
  return { title, robots: { index: false, follow: false } };
}

/** The route's locale; the [locale] layout has already rejected unknown values. */
export function routeLocale(value: string): Locale {
  return isLocale(value) ? value : "fa";
}

/** First value of a search param (Next passes string | string[] | undefined). */
export function firstParam(value: string | string[] | undefined): string {
  return (Array.isArray(value) ? value[0] : value) ?? "";
}
