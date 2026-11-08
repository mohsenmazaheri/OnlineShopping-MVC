import { describe, expect, it } from "vitest";
import { contentSecurityPolicy, securityHeaders, type HeaderOptions } from "./headers";

const production: HeaderOptions = { isDev: false, https: true };
const plainHttp: HeaderOptions = { isDev: false, https: false };
const development: HeaderOptions = { isDev: true, https: false };

const header = (options: HeaderOptions, key: string) => securityHeaders(options).find((h) => h.key === key)?.value;

describe("security headers", () => {
  it("locks everything to our own origin and forbids framing", () => {
    const csp = contentSecurityPolicy(production);
    expect(csp).toContain("default-src 'self'");
    expect(csp).toContain("object-src 'none'");
    expect(csp).toContain("frame-ancestors 'none'");
    expect(csp).not.toContain("unsafe-eval");
  });

  it("allows eval and websockets only in development (hot reload)", () => {
    const csp = contentSecurityPolicy(development);
    expect(csp).toContain("'unsafe-eval'");
    expect(csp).toContain("ws:");
  });

  it("loads no third-party fonts, scripts or images", () => {
    for (const directive of contentSecurityPolicy(production).split("; ")) {
      expect(directive).not.toMatch(/https?:\/\//);
    }
  });

  it("upgrades insecure requests and sends HSTS only on an HTTPS site", () => {
    expect(contentSecurityPolicy(production)).toContain("upgrade-insecure-requests");
    expect(header(production, "Strict-Transport-Security")).toContain("max-age=31536000");

    // Regression: on a plain-HTTP production build these broke every asset request.
    expect(contentSecurityPolicy(plainHttp)).not.toContain("upgrade-insecure-requests");
    expect(header(plainHttp, "Strict-Transport-Security")).toBeUndefined();
  });

  it("sets the standard hardening headers", () => {
    expect(header(production, "X-Content-Type-Options")).toBe("nosniff");
    expect(header(production, "X-Frame-Options")).toBe("DENY");
    expect(header(production, "Referrer-Policy")).toBe("strict-origin-when-cross-origin");
  });
});
