import { describe, expect, it } from "vitest";
import { cleanCode, format, toAsciiDigits } from "./digits";

describe("digits", () => {
  it("converts Persian and Arabic-Indic digits", () => {
    expect(toAsciiDigits("۰۹۱۲ ١٢٣ abc 45")).toBe("0912 123 abc 45");
  });

  it("cleans a one-time code to at most six ASCII digits", () => {
    expect(cleanCode(" ۴۸۲-۹۱۳ ")).toBe("482913");
    expect(cleanCode("1234567890")).toBe("123456");
    expect(cleanCode("12a3", 4)).toBe("123");
  });

  it("fills placeholders and leaves unknown ones visible", () => {
    expect(format("Send again in {seconds} s", { seconds: 42 })).toBe("Send again in 42 s");
    expect(format("{a} and {b}", { a: "x" })).toBe("x and {b}");
  });
});
