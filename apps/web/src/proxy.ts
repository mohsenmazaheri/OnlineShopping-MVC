import { NextResponse, type NextRequest } from "next/server";
import { apiBaseUrl } from "@/lib/api/base-url";
import { isApiPath, localeRedirectTarget } from "@/lib/routing";
import { securityHeaders } from "@/lib/security/headers";
import { indexingAllowed, siteUrl } from "@/lib/seo/config";
import { createStoreSettingsCache } from "@/lib/store/proxy-cache";

const storeSettings = createStoreSettingsCache(() => apiBaseUrl());

/**
 * Runs on every request at runtime (not at build), so one Docker image works for every shop:
 * API forwarding, locale redirects, security headers and the test-instance noindex all read live settings.
 */
export async function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  // The API sets its own (stricter) headers.
  if (isApiPath(pathname)) return NextResponse.rewrite(new URL(`${pathname}${search}`, apiBaseUrl()));

  const target = localeRedirectTarget(pathname, search, await storeSettings());
  const response = target ? NextResponse.redirect(new URL(target, request.url), 307) : NextResponse.next();

  const isDev = process.env.NODE_ENV !== "production";
  for (const { key, value } of securityHeaders({ isDev, https: siteUrl().startsWith("https://") })) {
    response.headers.set(key, value);
  }
  // Test/staging instances: keep every response out of search engines, whatever the page says.
  if (!indexingAllowed()) response.headers.set("X-Robots-Tag", "noindex, nofollow");

  return response;
}

export const config = {
  // Skip build assets and image optimization; everything else (pages, metadata routes, API) passes through.
  matcher: ["/((?!_next/static|_next/image).*)"],
};
