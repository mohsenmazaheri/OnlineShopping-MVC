import { describe, expect, it } from "vitest";
import { directionOf, isLocale, supportedLocales } from "./config";

describe("locale config", () => {
  it("recognises only supported locales", () => {
    for (const l of supportedLocales) expect(isLocale(l)).toBe(true);
    expect(isLocale("xx")).toBe(false);
    expect(isLocale(undefined)).toBe(false);
    expect(isLocale("FA")).toBe(false);
  });

  it("uses right-to-left only for Persian and Arabic", () => {
    expect(directionOf("fa")).toBe("rtl");
    expect(directionOf("ar")).toBe("rtl");
    expect(directionOf("en")).toBe("ltr");
    expect(directionOf("hy")).toBe("ltr");
    expect(directionOf("de")).toBe("ltr");
  });
});
