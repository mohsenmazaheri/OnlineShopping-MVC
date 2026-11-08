import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { supportedLocales } from "./config";
import { getDictionary } from "./dictionaries";

/** Error codes the API can return, read from the C# source so a new code without a translation fails here. */
function apiAccountErrorCodes(): string[] {
  const source = readFileSync(
    fileURLToPath(new URL("../../../../../src/OnlineShop.Application/Accounts/AccountValidators.cs", import.meta.url)),
    "utf8",
  );
  return [...source.matchAll(/= "account\.([a-z_]+)"/g)].map((m) => m[1]);
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
