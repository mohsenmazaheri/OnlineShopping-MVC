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
        signIn: { password: true, phoneCode: true, external: ["google"] },
        checkout: { guest: true, shippingCountries: ["IR"] },
      }),
    ).toEqual({
      name: "Shop",
      defaultLocale: "en",
      locales: ["fa", "en"],
      signUp: { email: true, phone: true },
      signIn: { password: true, phoneCode: true, external: ["google"] },
      checkout: { guest: true, shippingCountries: ["IR"] },
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
    expect(settings.signIn).toEqual({ password: true, phoneCode: false, external: [] });
  });

  it("falls back when the API is unreachable or returns nothing usable", () => {
    expect(toStoreSettings(null)).toEqual(fallbackStore);
    expect(toStoreSettings({ name: "Mine", locales: ["xx"] })).toEqual({ ...fallbackStore, name: "Mine" });
    expect(toStoreSettings({ name: "  ", locales: ["en"] }).name).toBe(fallbackStore.name);
  });
});

describe("external providers from the API", () => {
  it("keeps only providers this web build has buttons for", () => {
    const settings = toStoreSettings({ name: "S", locales: ["en"], signIn: { external: ["google", "myspace", "microsoft"] } });

    expect(settings.signIn.external).toEqual(["google", "microsoft"]);
  });

  it("offers none when the API says nothing", () => {
    expect(toStoreSettings({ name: "S", locales: ["en"] }).signIn.external).toEqual([]);
  });
});

describe("checkout settings from the API", () => {
  it("keeps guest checkout and valid country codes", () => {
    const settings = toStoreSettings({ name: "S", locales: ["en"], checkout: { guest: false, shippingCountries: ["AM", "de", "DE", "AM"] } });

    expect(settings.checkout).toEqual({ guest: false, shippingCountries: ["AM", "DE"] });
  });

  it("defaults to guest checkout on and no countries", () => {
    expect(toStoreSettings({ name: "S", locales: ["en"] }).checkout).toEqual({ guest: true, shippingCountries: [] });
  });
});
