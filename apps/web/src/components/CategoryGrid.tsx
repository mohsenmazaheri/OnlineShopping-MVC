import type { CategorySummary } from "@/lib/api/storefront";

type Props = {
  categories: CategorySummary[];
  heading: string;
  emptyText: string;
};

/** Category cards for the home page. Category pages arrive in phase 6; until then cards are not links. */
export function CategoryGrid({ categories, heading, emptyText }: Props) {
  return (
    <section aria-labelledby="categories-heading" className="mx-auto w-full max-w-6xl px-4 py-6">
      <h2 id="categories-heading" className="mb-4 text-lg font-bold">
        {heading}
      </h2>
      {categories.length === 0 ? (
        <p className="text-muted">{emptyText}</p>
      ) : (
        <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {categories.map((category) => (
            <li key={category.id} className="rounded-2xl border border-border bg-surface p-4">
              <h3 className="font-semibold">{category.name}</h3>
              {category.subCategories.length > 0 && (
                <ul className="mt-2 space-y-1 text-sm text-muted">
                  {category.subCategories.map((sub) => (
                    <li key={sub.id}>{sub.name}</li>
                  ))}
                </ul>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export function CategoryGridSkeleton({ label }: { label: string }) {
  return (
    <div role="status" aria-label={label} className="mx-auto grid w-full max-w-6xl grid-cols-2 gap-3 px-4 py-6 sm:grid-cols-3 lg:grid-cols-4">
      {Array.from({ length: 8 }, (_, i) => (
        <div key={i} className="h-24 animate-pulse rounded-2xl bg-border" />
      ))}
    </div>
  );
}
