import { describe, expect, it } from "vitest";
import { fallbackStore, toStoreSettings } from "./settings";

describe("store settings from the API", () => {
  it("keeps valid API settings", () => {
    expect(
      toStoreSettings({
        name: " Shop ",
        defaultLocale: "en",
        locales: ["fa", "en"],
        signUp: { email: true, phone: true },
        signIn: { password: true, phoneCode: true },
      }),
    ).toEqual({
      name: "Shop",
      defaultLocale: "en",
      locales: ["fa", "en"],
      signUp: { email: true, phone: true },
      signIn: { password: true, phoneCode: true },
    });
  });

  it("drops locales this web build cannot render, and repeats", () => {
    expect(toStoreSettings({ name: "S", defaultLocale: "fa", locales: ["fa", "xx", "fa", "de"] }).locales).toEqual(["fa", "de"]);
  });

  it("moves the default locale to the first offered one when it is not offered", () => {
    expect(toStoreSettings({ name: "S", defaultLocale: "ar", locales: ["hy", "en"] }).defaultLocale).toBe("hy");
  });

  it("assumes email sign-up only when the API does not say", () => {
    const settings = toStoreSettings({ name: "S", locales: ["en"] });
    expect(settings.signUp).toEqual({ email: true, phone: false });
    expect(settings.signIn).toEqual({ password: true, phoneCode: false });
  });

  it("falls back when the API is unreachable or returns nothing usable", () => {
    expect(toStoreSettings(null)).toEqual(fallbackStore);
    expect(toStoreSettings({ name: "Mine", locales: ["xx"] })).toEqual({ ...fallbackStore, name: "Mine" });
    expect(toStoreSettings({ name: "  ", locales: ["en"] }).name).toBe(fallbackStore.name);
  });
});
