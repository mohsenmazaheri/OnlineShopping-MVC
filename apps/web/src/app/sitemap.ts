import type { MetadataRoute } from "next";
import { buildSitemap } from "@/lib/seo/sitemap";
import { loadStore } from "@/lib/store/load";

/** Phase 1: home pages. Later phases add categories, products, brands and posts (docs/SEO-MARKETING.md §6). */
export default async function sitemap(): Promise<MetadataRoute.Sitemap> {
  const store = await loadStore();
  return buildSitemap(["/"], store.locales, store.defaultLocale);
}
