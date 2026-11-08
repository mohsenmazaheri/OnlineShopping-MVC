import { describe, expect, it } from "vitest";
import { buildAlternates, buildRobots, indexingAllowed, siteUrl } from "./config";

const prod = { SITE_URL: "https://shop.example.com/", SEO_ALLOW_INDEXING: "true" };

describe("SEO config", () => {
  it("normalises the site URL", () => {
    expect(siteUrl(prod)).toBe("https://shop.example.com");
    expect(siteUrl({})).toBe("http://localhost:3000");
  });

  it("allows indexing only when explicitly enabled", () => {
    expect(indexingAllowed(prod)).toBe(true);
    expect(indexingAllowed({})).toBe(false);
    expect(indexingAllowed({ SEO_ALLOW_INDEXING: "yes" })).toBe(false);
  });

  it("blocks all crawling on test instances", () => {
    expect(buildRobots({})).toEqual({ rules: { userAgent: "*", disallow: "/" } });
  });

  it("blocks private areas and points to the sitemap in production", () => {
    const robots = buildRobots(prod);
    expect(robots.sitemap).toBe("https://shop.example.com/sitemap.xml");
    const rules = robots.rules as { disallow: string[] };
    expect(rules.disallow).toEqual(expect.arrayContaining(["/api/", "/cart", "/checkout", "/account", "/admin"]));
  });

  it("builds canonical and hreflang alternates with x-default", () => {
    const alternates = buildAlternates("/p/12/phone", "en", ["fa", "en"], "fa", prod);
    expect(alternates.canonical).toBe("https://shop.example.com/en/p/12/phone");
    expect(alternates.languages).toEqual({
      fa: "https://shop.example.com/fa/p/12/phone",
      en: "https://shop.example.com/en/p/12/phone",
      "x-default": "https://shop.example.com/fa/p/12/phone",
    });
  });

  it("has no trailing slash on the home page", () => {
    expect(buildAlternates("/", "fa", ["fa"], "fa", prod).canonical).toBe("https://shop.example.com/fa");
  });

  it("points x-default at the first locale when the default one is not offered", () => {
    const alternates = buildAlternates("/", "en", ["en", "de"], "fa", prod);
    expect((alternates.languages as Record<string, string>)["x-default"]).toBe("https://shop.example.com/en");
  });
});
