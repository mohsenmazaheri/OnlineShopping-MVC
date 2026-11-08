import Link from "next/link";
import { Suspense } from "react";
import { getCurrentUser } from "@/lib/account/session";
import type { Locale } from "@/lib/i18n/config";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { loadStore } from "@/lib/store/load";

type Props = { locale: Locale; texts: Dictionary["header"] };

/** Per-visitor link: reads the session, so it streams separately and never ends up in a shared cache. */
async function AccountLink({ locale, texts }: Props) {
  const user = await getCurrentUser();
  return user ? (
    <Link href={`/${locale}/account`} className="truncate text-sm font-medium hover:text-primary">
      {user.displayName || texts.myAccount}
    </Link>
  ) : (
    <Link href={`/${locale}/account/login`} className="rounded-lg border border-border px-3 py-1.5 text-sm font-medium hover:border-primary">
      {texts.signIn}
    </Link>
  );
}

async function StoreName({ locale }: { locale: Locale }) {
  const store = await loadStore();
  return (
    <Link href={`/${locale}`} className="text-xl font-extrabold text-primary">
      {store.name}
    </Link>
  );
}

export function SiteHeader({ locale, texts }: Props) {
  return (
    <header className="border-b border-border bg-surface">
      {/* Fixed height and placeholders keep the layout still while the parts stream in (no CLS). */}
      <div className="mx-auto flex h-16 max-w-6xl items-center justify-between gap-4 px-4">
        <Suspense fallback={<span className="h-6 w-32 animate-pulse rounded bg-border" />}>
          <StoreName locale={locale} />
        </Suspense>
        <Suspense fallback={<span className="h-8 w-28 animate-pulse rounded bg-border" />}>
          <AccountLink locale={locale} texts={texts} />
        </Suspense>
      </div>
    </header>
  );
}
