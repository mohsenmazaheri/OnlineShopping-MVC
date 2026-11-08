import { readdirSync, readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { supportedLocales } from "./config";
import { getDictionary } from "./dictionaries";

/**
 * Error codes the API can return, read from every C# file of the Application project (not bin/obj), so a new code
 * without a translation fails here.
 */
function apiAccountErrorCodes(): string[] {
  const root = fileURLToPath(new URL("../../../../../src/OnlineShop.Application/", import.meta.url));
  const files = readdirSync(root, { recursive: true, encoding: "utf8" }).filter(
    (file) => file.endsWith(".cs") && !/(^|[\\/])(bin|obj)[\\/]/.test(file),
  );
  const codes = files.flatMap((file) => [...readFileSync(root + file, "utf8").matchAll(/= "account\.([a-z0-9_]+)"/g)].map((m) => m[1]));
  return [...new Set(codes)];
}

describe("API error code translations", () => {
  it("finds the API's account error codes", () => {
    expect(apiAccountErrorCodes().length).toBeGreaterThan(10);
  });

  it.each(supportedLocales)("every account error code has a %s message", async (locale) => {
    const errors = (await getDictionary(locale)).account.errors as Record<string, string>;
    const missing = apiAccountErrorCodes().filter((code) => !errors[code]);

    expect(missing).toEqual([]);
  });
});
