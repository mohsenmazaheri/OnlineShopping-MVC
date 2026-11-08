/** Server-side base URL of the .NET API (never exposed to the browser). Read at request time. */
export function apiBaseUrl(env: Record<string, string | undefined> = process.env): string {
  return (env.API_INTERNAL_URL ?? "http://localhost:5087").replace(/\/+$/, "");
}
