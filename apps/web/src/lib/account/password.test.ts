import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { PASSWORD_MIN_LENGTH } from "./password";

describe("password rules", () => {
  it("uses the same minimum length as the API", () => {
    const source = readFileSync(
      fileURLToPath(new URL("../../../../../src/OnlineShopping.Application/Accounts/AccountValidators.cs", import.meta.url)),
      "utf8",
    );
    const api = Number(/const int MinLength = (\d+);/.exec(source)?.[1]);

    expect(PASSWORD_MIN_LENGTH).toBe(api);
  });
});
