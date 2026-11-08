import type { Metadata } from "next";
import { isLocale, type Locale } from "@/lib/i18n/config";
import { getDictionary } from "@/lib/i18n/dictionaries";

export const metadata: Metadata = { robots: { index: false, follow: false } };

/** Precached by the service worker and shown when a page cannot be loaded without a network. */
export default async function OfflinePage({ params }: PageProps<"/[locale]/offline">) {
  const { locale } = await params;
  const dict = await getDictionary(isLocale(locale) ? locale : ("fa" satisfies Locale));
  return (
    <main className="mx-auto flex max-w-md flex-1 flex-col items-center justify-center gap-2 px-4 text-center">
      <h1 className="text-xl font-bold">{dict.offline.title}</h1>
      <p className="text-muted">{dict.offline.body}</p>
    </main>
  );
}
