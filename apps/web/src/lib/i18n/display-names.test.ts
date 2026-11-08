import { describe, expect, it } from "vitest";
import { nativeLanguageName, regionName } from "./display-names";

describe("display names", () => {
  it("names each language in itself", () => {
    expect(nativeLanguageName("de")).toBe("Deutsch");
    expect(nativeLanguageName("fa")).toBe("فارسی");
  });

  it("names countries in the reader's language", () => {
    expect(regionName("DE", "en")).toBe("Germany");
    expect(regionName("IR", "fa")).toBe("ایران");
  });

  it("falls back to the code for unknown or malformed input", () => {
    expect(regionName("XY", "en")).toBe("XY");
    expect(regionName("DE", "not a locale!")).toBe("DE");
  });
});
