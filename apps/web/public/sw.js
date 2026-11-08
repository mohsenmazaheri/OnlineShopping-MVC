/*
 * PWA service worker.
 * - Pages (navigations): network first, never cached — prices and stock must always be fresh for users and crawlers.
 *   Without a network, the precached offline page for the visitor's locale is shown.
 * - Build assets (/_next/static/, content-hashed): cache first.
 * - API calls and everything else: not touched.
 */
const VERSION = "v1";
const STATIC_CACHE = `static-${VERSION}`;
const OFFLINE_CACHE = `offline-${VERSION}`;

const params = new URL(self.location.href).searchParams;
const locales = (params.get("locales") || "fa").split(",").filter(Boolean);
const offlineUrl = (locale) => `/${locale}/offline`;

self.addEventListener("install", (event) => {
  event.waitUntil(
    caches
      .open(OFFLINE_CACHE)
      .then((cache) => cache.addAll(locales.map(offlineUrl)))
      .then(() => self.skipWaiting()),
  );
});

self.addEventListener("activate", (event) => {
  event.waitUntil(
    caches
      .keys()
      .then((keys) => Promise.all(keys.filter((k) => k !== STATIC_CACHE && k !== OFFLINE_CACHE).map((k) => caches.delete(k))))
      .then(() => self.clients.claim()),
  );
});

self.addEventListener("fetch", (event) => {
  const request = event.request;
  if (request.method !== "GET") return;
  const url = new URL(request.url);
  if (url.origin !== self.location.origin) return;

  if (request.mode === "navigate") {
    event.respondWith(
      fetch(request).catch(async () => {
        const first = url.pathname.split("/")[1];
        const locale = locales.includes(first) ? first : locales[0];
        return (await caches.match(offlineUrl(locale))) || Response.error();
      }),
    );
    return;
  }

  if (url.pathname.startsWith("/_next/static/")) {
    event.respondWith(
      caches.open(STATIC_CACHE).then(async (cache) => {
        const cached = await cache.match(request);
        if (cached) return cached;
        const response = await fetch(request);
        if (response.ok) cache.put(request, response.clone());
        return response;
      }),
    );
  }
});
