import { describe, expect, it } from "vitest";
import { accountRedirectTarget, isApiPath, localeRedirectTarget } from "./routing";

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

describe("account pages need a session", () => {
  it("sends signed-out visitors to the login page with a return address", () => {
    expect(accountRedirectTarget("/fa/account", "", false)).toBe("/fa/account/login?returnUrl=%2Ffa%2Faccount");
    expect(accountRedirectTarget("/en/account/orders", "?page=2", false)).toBe(
      "/en/account/login?returnUrl=%2Fen%2Faccount%2Forders%3Fpage%3D2",
    );
  });

  it("lets visitors with a session cookie through (the page verifies it)", () => {
    expect(accountRedirectTarget("/fa/account", "", true)).toBeNull();
  });

  it.each(["login", "register", "forgot-password", "reset-password", "confirm-email"])("keeps /account/%s public", (page) => {
    expect(accountRedirectTarget(`/fa/account/${page}`, "", false)).toBeNull();
  });

  it("ignores other pages", () => {
    expect(accountRedirectTarget("/fa", "", false)).toBeNull();
    expect(accountRedirectTarget("/fa/accounting", "", false)).toBeNull();
    expect(accountRedirectTarget("/xx/account", "", false)).toBeNull();
  });
});
