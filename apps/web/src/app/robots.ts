import type { MetadataRoute } from "next";
import { connection } from "next/server";
import { buildRobots } from "@/lib/seo/config";

/** Rendered per request: SEO_ALLOW_INDEXING and SITE_URL differ between test and production containers. */
export default async function robots(): Promise<MetadataRoute.Robots> {
  await connection();
  return buildRobots();
}
