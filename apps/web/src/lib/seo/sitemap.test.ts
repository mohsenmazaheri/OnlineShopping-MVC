import { describe, expect, it } from "vitest";
import { buildSitemap } from "./sitemap";

describe("sitemap", () => {
  it("lists every page in every locale with its alternates", () => {
    const entries = buildSitemap(["/", "/about"], ["fa", "en"], "fa", { SITE_URL: "https://s.test" });

    expect(entries.map((e) => e.url)).toEqual([
      "https://s.test/fa",
      "https://s.test/en",
      "https://s.test/fa/about",
      "https://s.test/en/about",
    ]);
    expect(entries[1].alternates?.languages).toMatchObject({ fa: "https://s.test/fa", "x-default": "https://s.test/fa" });
  });
});
