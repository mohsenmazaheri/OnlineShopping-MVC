/**
 * Security headers for every page served by the Next.js app.
 *
 * The CSP cannot use nonces: they require dynamic rendering of every page, which rules out the static
 * shells/ISR we rely on for speed and SEO. Inline scripts are therefore allowed ('unsafe-inline'),
 * while everything else is locked to our own origin — no third-party scripts, fonts or CDNs
 * (consent-gated marketing tags will extend script-src explicitly in phase 10).
 */
export type HeaderOptions = {
  /** Development server: allow eval and websockets for hot reload. */
  isDev: boolean;
  /** The public site is served over HTTPS (SITE_URL starts with https://). */
  https: boolean;
};

export function contentSecurityPolicy({ isDev, https }: HeaderOptions): string {
  const directives: Record<string, string[]> = {
    "default-src": ["'self'"],
    "script-src": ["'self'", "'unsafe-inline'", ...(isDev ? ["'unsafe-eval'"] : [])],
    "style-src": ["'self'", "'unsafe-inline'"],
    "img-src": ["'self'", "data:", "blob:"],
    "font-src": ["'self'"],
    "connect-src": ["'self'", ...(isDev ? ["ws:"] : [])],
    "media-src": ["'self'"],
    "manifest-src": ["'self'"],
    "worker-src": ["'self'"],
    "object-src": ["'none'"],
    "base-uri": ["'self'"],
    "form-action": ["'self'"],
    "frame-ancestors": ["'none'"],
  };

  const policy = Object.entries(directives).map(([name, values]) => `${name} ${values.join(" ")}`);
  // On a plain-HTTP site (local Docker test) this would rewrite asset URLs to https:// and break them.
  if (https) policy.push("upgrade-insecure-requests");
  return policy.join("; ");
}

export function securityHeaders(options: HeaderOptions): { key: string; value: string }[] {
  const headers = [
    { key: "Content-Security-Policy", value: contentSecurityPolicy(options) },
    { key: "X-Content-Type-Options", value: "nosniff" },
    { key: "X-Frame-Options", value: "DENY" },
    { key: "Referrer-Policy", value: "strict-origin-when-cross-origin" },
    { key: "Permissions-Policy", value: "camera=(), microphone=(), geolocation=(), payment=(self)" },
    { key: "Cross-Origin-Opener-Policy", value: "same-origin" },
  ];
  if (options.https) {
    headers.push({ key: "Strict-Transport-Security", value: "max-age=31536000; includeSubDomains" });
  }
  return headers;
}
