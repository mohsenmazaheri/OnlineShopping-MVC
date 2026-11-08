import { describe, expect, it } from "vitest";
import { safeReturnPath } from "./return-path";

describe("return path after sign-in (open-redirect protection)", () => {
  it("accepts paths inside this site and locale", () => {
    expect(safeReturnPath("/fa/account", "fa")).toBe("/fa/account");
    expect(safeReturnPath("/fa/p/12/phone?color=red", "fa")).toBe("/fa/p/12/phone?color=red");
    expect(safeReturnPath("/fa", "fa")).toBe("/fa");
  });

  it.each([
    ["missing", null],
    ["absolute URL", "https://evil.example/fa"],
    ["protocol-relative", "//evil.example/fa"],
    ["backslash trick", "/\\evil.example"],
    ["other locale", "/en/account"],
    ["locale prefix lookalike", "/fake/account"],
    ["not a path", "javascript:alert(1)"],
    ["back to the login page", "/fa/account/login?returnUrl=/fa"],
  ])("falls back to the home page for %s", (_, value) => {
    expect(safeReturnPath(value, "fa")).toBe("/fa");
  });
});
