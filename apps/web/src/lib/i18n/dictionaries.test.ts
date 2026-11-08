import { describe, expect, it } from "vitest";
import { supportedLocales } from "./config";
import { getDictionary } from "./dictionaries";

function keyPaths(value: unknown, prefix = ""): string[] {
  if (typeof value !== "object" || value === null) return [prefix];
  return Object.entries(value).flatMap(([k, v]) => keyPaths(v, prefix ? `${prefix}.${k}` : k));
}

function leaves(value: unknown): unknown[] {
  if (typeof value !== "object" || value === null) return [value];
  return Object.values(value).flatMap(leaves);
}

describe("UI dictionaries", () => {
  it("every locale has exactly the keys of the Persian dictionary", async () => {
    const reference = keyPaths(await getDictionary("fa")).sort();
    for (const locale of supportedLocales) {
      expect(keyPaths(await getDictionary(locale)).sort(), locale).toEqual(reference);
    }
  });

  it("no translation is empty", async () => {
    for (const locale of supportedLocales) {
      for (const text of leaves(await getDictionary(locale))) {
        expect(typeof text === "string" && text.trim().length > 0, `${locale}: ${String(text)}`).toBe(true);
      }
    }
  });
});
