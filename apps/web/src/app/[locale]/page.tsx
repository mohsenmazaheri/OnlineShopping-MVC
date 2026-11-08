import type { Metadata } from "next";
import { Suspense } from "react";
import { CategoryGrid, CategoryGridSkeleton } from "@/components/CategoryGrid";
import { getCategoryMenu } from "@/lib/api/storefront";
import { isLocale, type Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";
import { buildAlternates } from "@/lib/seo/config";
import { loadStore } from "@/lib/store/load";

export async function generateMetadata({ params }: PageProps<"/[locale]">): Promise<Metadata> {
  const { locale } = await params;
  if (!isLocale(locale)) return {};
  const [dict, store] = await Promise.all([getDictionary(locale), loadStore()]);
  return {
    title: { absolute: store.name },
    description: dict.meta.description,
    alternates: buildAlternates("/", locale, store.locales, store.defaultLocale),
    openGraph: { siteName: store.name, locale, type: "website" },
  };
}

/**
 * Read per request (the build has no API), then cached by `use cache` for hours and revalidated on change.
 * Streamed in the same HTML response, so crawlers see the full content.
 */
async function HomeContent({ dict }: { dict: Dictionary }) {
  const store = await loadStore();
  // An unreachable API must not break the page: show a friendly notice instead.
  const categories = await getCategoryMenu().catch(() => null);

  return (
    <>
      <header className="border-b border-border bg-surface">
        <div className="mx-auto max-w-6xl px-4 py-4">
          <h1 className="text-xl font-extrabold text-primary">{store.name}</h1>
        </div>
      </header>
      {categories ? (
        <CategoryGrid categories={categories} heading={dict.home.categories} emptyText={dict.home.noCategories} />
      ) : (
        <p role="alert" className="mx-auto max-w-6xl px-4 py-6 text-muted">
          {dict.home.unavailable}
        </p>
      )}
    </>
  );
}

export default async function HomePage({ params }: PageProps<"/[locale]">) {
  const { locale } = await params;
  const dict = await getDictionary(locale as Locale);

  return (
    <main className="flex-1">
      <Suspense fallback={<CategoryGridSkeleton label={dict.home.loading} />}>
        <HomeContent dict={dict} />
      </Suspense>
    </main>
  );
}
