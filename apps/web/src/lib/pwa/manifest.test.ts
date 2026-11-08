import { describe, expect, it } from "vitest";
import { buildManifest } from "./manifest";

describe("web app manifest", () => {
  it("is installable: standalone display and 192/512 icons", () => {
    const manifest = buildManifest({ name: "Shop", defaultLocale: "fa", locales: ["fa", "en"] });

    expect(manifest.display).toBe("standalone");
    expect(manifest.icons?.map((i) => i.sizes)).toEqual(expect.arrayContaining(["192x192", "512x512"]));
    expect(manifest.icons?.some((i) => i.purpose === "maskable")).toBe(true);
  });

  it("starts in the default locale with its text direction", () => {
    expect(buildManifest({ name: "S", defaultLocale: "fa", locales: ["fa"] })).toMatchObject({ start_url: "/fa", lang: "fa", dir: "rtl" });
    expect(buildManifest({ name: "S", defaultLocale: "en", locales: ["en"] })).toMatchObject({ start_url: "/en", dir: "ltr" });
  });

  it("keeps the short name short", () => {
    expect(buildManifest({ name: "A Very Long Store Name", defaultLocale: "en", locales: ["en"] }).short_name).toHaveLength(12);
  });
});
