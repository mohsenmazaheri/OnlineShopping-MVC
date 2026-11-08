import { describe, expect, it } from "vitest";
import { messageFor, parseProblem, problemMessage, type ErrorTexts } from "./problems";

const texts: ErrorTexts = {
  generic: "Something went wrong",
  network: "Offline",
  too_many_requests: "Slow down",
  invalid_credentials: "Wrong details",
  password_too_short: "Too short",
};

describe("problem details", () => {
  it("reads the business code and field errors", () => {
    const problem = parseProblem(400, {
      title: "x",
      code: "account.invalid_credentials",
      errors: { password: ["account.password_too_short", 7], email: "not-an-array" },
    });

    expect(problem).toEqual({
      status: 400,
      code: "account.invalid_credentials",
      fieldErrors: { password: ["account.password_too_short"] },
    });
  });

  it("copes with empty and non-object bodies", () => {
    expect(parseProblem(500, null)).toEqual({ status: 500, fieldErrors: {} });
    expect(parseProblem(502, "Bad Gateway")).toEqual({ status: 502, fieldErrors: {} });
  });

  it("translates codes by their name without the area prefix, never showing raw codes", () => {
    expect(messageFor("account.invalid_credentials", texts)).toBe("Wrong details");
    expect(messageFor("network", texts)).toBe("Offline");
    expect(messageFor("account.something_new", texts)).toBe("Something went wrong");
    expect(messageFor(undefined, texts)).toBe("Something went wrong");
  });

  it("prefers rate limiting, then the business code, then the first field error", () => {
    expect(problemMessage({ status: 429, code: "account.invalid_credentials", fieldErrors: {} }, texts)).toBe("Slow down");
    expect(problemMessage({ status: 400, code: "account.invalid_credentials", fieldErrors: {} }, texts)).toBe("Wrong details");
    expect(problemMessage({ status: 400, fieldErrors: { password: ["account.password_too_short"] } }, texts)).toBe("Too short");
    expect(problemMessage({ status: 500, fieldErrors: {} }, texts)).toBe("Something went wrong");
  });
});
