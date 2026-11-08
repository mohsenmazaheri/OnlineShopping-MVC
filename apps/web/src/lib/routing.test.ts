import { describe, expect, it } from "vitest";
import { isApiPath, localeRedirectTarget } from "./routing";

const store = { defaultLocale: "fa" as const, locales: ["fa" as const, "en" as const] };

describe("locale redirects", () => {
  it("sends the root to the default locale", () => {
    expect(localeRedirectTarget("/", "", store)).toBe("/fa");
  });

  it("prefixes paths without a locale and keeps the query string", () => {
    expect(localeRedirectTarget("/c/phones", "?sort=price", { ...store, defaultLocale: "en" })).toBe("/en/c/phones?sort=price");
  });

  it("leaves paths in an offered locale alone", () => {
    expect(localeRedirectTarget("/en", "", store)).toBeNull();
    expect(localeRedirectTarget("/fa/p/1/x", "", store)).toBeNull();
  });

  it("moves a locale the store does not offer to the default locale", () => {
    expect(localeRedirectTarget("/de/p/1/x", "?a=1", store)).toBe("/fa/p/1/x?a=1");
    expect(localeRedirectTarget("/hy", "", store)).toBe("/fa");
  });

  it.each([
    "/api/v1/store",
    "/api/revalidate",
    "/_next/static/a.js",
    "/sw.js",
    "/robots.txt",
    "/sitemap.xml",
    "/manifest.webmanifest",
    "/icon/192",
    "/logo.png",
  ])("does not touch %s", (path) => {
    expect(localeRedirectTarget(path, "", store)).toBeNull();
  });
});

describe("API forwarding", () => {
  it("forwards only versioned API paths", () => {
    expect(isApiPath("/api/v1/store")).toBe(true);
    expect(isApiPath("/api/revalidate")).toBe(false);
    expect(isApiPath("/fa")).toBe(false);
  });
});
