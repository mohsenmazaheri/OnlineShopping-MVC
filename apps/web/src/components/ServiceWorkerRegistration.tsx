"use client";

import { useEffect } from "react";

/**
 * Registers the PWA service worker (public/sw.js) in production builds only, so development
 * never serves stale assets. `locales` tells the worker which offline pages to precache.
 */
export function ServiceWorkerRegistration({ locales }: { locales: string[] }) {
  useEffect(() => {
    if (process.env.NODE_ENV !== "production" || !("serviceWorker" in navigator)) return;
    const url = `/sw.js?locales=${encodeURIComponent(locales.join(","))}`;
    navigator.serviceWorker.register(url, { scope: "/" }).catch(() => {
      // The site works fully without the worker; installability/offline page are a bonus.
    });
  }, [locales]);

  return null;
}
