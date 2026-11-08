import type { MetadataRoute } from "next";
import { buildManifest } from "@/lib/pwa/manifest";
import { loadStore } from "@/lib/store/load";

export default async function manifest(): Promise<MetadataRoute.Manifest> {
  return buildManifest(await loadStore());
}
