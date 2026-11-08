import { describe, expect, it } from "vitest";
import { fallbackStore, toStoreSettings } from "./settings";

describe("store settings from the API", () => {
  it("keeps valid API settings", () => {
    expect(toStoreSettings({ name: " Shop ", defaultLocale: "en", locales: ["fa", "en"] })).toEqual({
      name: "Shop",
      defaultLocale: "en",
      locales: ["fa", "en"],
    });
  });

  it("drops locales this web build cannot render, and repeats", () => {
    expect(toStoreSettings({ name: "S", defaultLocale: "fa", locales: ["fa", "xx", "fa", "de"] }).locales).toEqual(["fa", "de"]);
  });

  it("moves the default locale to the first offered one when it is not offered", () => {
    expect(toStoreSettings({ name: "S", defaultLocale: "ar", locales: ["hy", "en"] }).defaultLocale).toBe("hy");
  });

  it("falls back when the API is unreachable or returns nothing usable", () => {
    expect(toStoreSettings(null)).toEqual(fallbackStore);
    expect(toStoreSettings({ name: "Mine", locales: ["xx"] })).toEqual({ ...fallbackStore, name: "Mine" });
    expect(toStoreSettings({ name: "  ", locales: ["en"] }).name).toBe(fallbackStore.name);
  });
});
