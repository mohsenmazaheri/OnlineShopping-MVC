import "server-only";
import { cookies } from "next/headers";
import { apiBaseUrl } from "@/lib/api/base-url";
import type { components } from "@/lib/api/schema";
import { sessionCookieName } from "./cookie";

export type CurrentUser = components["schemas"]["CurrentUser"];


/**
 * The signed-in user for server rendering, or null. Forwards only the session cookie to the API.
 * Never cached: the answer is per user and must not leak between visitors.
 */
export async function getCurrentUser(fetchImpl: typeof fetch = fetch): Promise<CurrentUser | null> {
  const session = (await cookies()).get(sessionCookieName);
  if (!session) return null;

  try {
    const response = await fetchImpl(`${apiBaseUrl()}/api/v1/account/me`, {
      headers: { Accept: "application/json", Cookie: `${sessionCookieName}=${session.value}` },
      cache: "no-store",
      signal: AbortSignal.timeout(5000),
    });
    return response.ok ? ((await response.json()) as CurrentUser) : null;
  } catch {
    return null;
  }
}
