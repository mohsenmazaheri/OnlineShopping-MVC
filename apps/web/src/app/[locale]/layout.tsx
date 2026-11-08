import type { Metadata, Viewport } from "next";
import { notFound } from "next/navigation";
import { Suspense } from "react";
import { ServiceWorkerRegistration } from "@/components/ServiceWorkerRegistration";
import { SiteHeader } from "@/components/SiteHeader";
import { getDictionary } from "@/lib/i18n/dictionaries";
import { directionOf, isLocale, supportedLocales } from "@/lib/i18n/config";
import { indexingAllowed, siteUrl } from "@/lib/seo/config";
import { loadStore } from "@/lib/store/load";
import "../globals.css";

/** lang/dir are known at build time; everything store-specific is read per request (one image for all shops). */
export function generateStaticParams() {
  return supportedLocales.map((locale) => ({ locale }));
}

export async function generateMetadata(): Promise<Metadata> {
  const store = await loadStore();
  return {
    metadataBase: new URL(siteUrl()),
    title: { default: store.name, template: `%s | ${store.name}` },
    applicationName: store.name,
    // Test/staging instances are never indexed (SEO_ALLOW_INDEXING must be "true" in production).
    robots: indexingAllowed() ? undefined : { index: false, follow: false },
  };
}

export const viewport: Viewport = {
  width: "device-width",
  initialScale: 1,
  themeColor: [
    { media: "(prefers-color-scheme: light)", color: "#ffffff" },
    { media: "(prefers-color-scheme: dark)", color: "#1c1c22" },
  ],
};

/** Also the page's runtime marker: it reads store settings inside Suspense, so the static shell still prerenders. */
async function StoreServiceWorker() {
  const store = await loadStore();
  return <ServiceWorkerRegistration locales={store.locales} />;
}

export default async function LocaleLayout({ children, params }: LayoutProps<"/[locale]">) {
  const { locale } = await params;
  if (!isLocale(locale)) notFound();

  const dict = await getDictionary(locale);

  return (
    <html lang={locale} dir={directionOf(locale)} className="h-full antialiased">
      <body className="flex min-h-full flex-col">
        <SiteHeader locale={locale} texts={dict.header} />
        {children}
        <Suspense fallback={null}>
          <StoreServiceWorker />
        </Suspense>
      </body>
    </html>
  );
}
