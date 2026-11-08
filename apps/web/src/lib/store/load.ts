import "server-only";
import { connection } from "next/server";
import { getStoreInfo } from "@/lib/api/storefront";
import { toStoreSettings, type StoreSettings } from "./settings";

/**
 * Store settings for the current request. `connection()` defers rendering to request time (the build has no API
 * and the image is shared by all shops); the API response itself is cached by `use cache` in getStoreInfo.
 */
export async function loadStore(): Promise<StoreSettings> {
  await connection();
  const info = await getStoreInfo().catch(() => null);
  return toStoreSettings(info);
}
