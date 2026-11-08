import { toStoreSettings, type StoreSettings, type StoreInfoLike } from "./settings";

const successTtlMs = 60_000;
const failureTtlMs = 5_000;
const timeoutMs = 2_000;

type Entry = { value: StoreSettings; expires: number };

/**
 * Store settings for proxy.ts (locale redirects run on every request). Keeps the API answer in memory for a minute,
 * and a failure for only a few seconds so a restarted API is picked up quickly.
 */
export function createStoreSettingsCache(
  apiBaseUrl: () => string,
  fetchImpl: typeof fetch = fetch,
  now: () => number = Date.now,
) {
  let entry: Entry | null = null;
  let pending: Promise<StoreSettings> | null = null;

  async function load(): Promise<StoreSettings> {
    try {
      const response = await fetchImpl(`${apiBaseUrl()}/api/v1/store`, {
        headers: { Accept: "application/json" },
        signal: AbortSignal.timeout(timeoutMs),
      });
      if (!response.ok) throw new Error(`status ${response.status}`);
      const value = toStoreSettings((await response.json()) as StoreInfoLike);
      entry = { value, expires: now() + successTtlMs };
      return value;
    } catch {
      const value = toStoreSettings(null);
      entry = { value, expires: now() + failureTtlMs };
      return value;
    }
  }

  return async function get(): Promise<StoreSettings> {
    if (entry && entry.expires > now()) return entry.value;
    // One request at a time, however many visitors arrive while it is in flight.
    pending ??= load().finally(() => {
      pending = null;
    });
    return pending;
  };
}
