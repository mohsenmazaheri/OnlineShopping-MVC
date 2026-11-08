import { redirect } from "next/navigation";
import { Suspense } from "react";
import { LogoutButton, ResendConfirmationButton } from "@/components/account/AccountActions";
import { AuthCard } from "@/components/account/FormParts";
import { accountMetadata, routeLocale } from "@/lib/account/page-helpers";
import { getCurrentUser } from "@/lib/account/session";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";

export async function generateMetadata({ params }: PageProps<"/[locale]/account">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.accountTitle);
}

/** Phase 2a: who is signed in, email confirmation, sign out. Profile, addresses and security follow in 2e. */
async function AccountSummary({ locale, texts }: { locale: Locale; texts: Dictionary["account"] }) {
  const user = await getCurrentUser();
  // proxy.ts already redirects visitors without a session cookie; this covers expired or invalid sessions.
  if (!user) redirect(`/${locale}/account/login?returnUrl=${encodeURIComponent(`/${locale}/account`)}`);

  return (
    <div className="flex flex-col gap-4">
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 text-sm">
        {user.displayName && (
          <>
            <dt className="text-muted">{texts.displayName}</dt>
            <dd>{user.displayName}</dd>
          </>
        )}
        {user.email && (
          <>
            <dt className="text-muted">{texts.email}</dt>
            <dd dir="ltr" className="text-start">{user.email}</dd>
          </>
        )}
        {user.phoneNumber && (
          <>
            <dt className="text-muted">{texts.phone}</dt>
            <dd dir="ltr" className="text-start">{user.phoneNumber}</dd>
          </>
        )}
      </dl>
      {user.email && !user.emailConfirmed && (
        <div className="rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200">
          <p className="mb-1">{texts.emailNotConfirmed}</p>
          <ResendConfirmationButton texts={texts} />
        </div>
      )}
      <LogoutButton locale={locale} texts={texts} />
    </div>
  );
}

export default async function AccountPage({ params }: PageProps<"/[locale]/account">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.accountTitle}>
      <Suspense fallback={<p role="status">{dict.account.working}</p>}>
        <AccountSummary locale={locale} texts={dict.account} />
      </Suspense>
    </AuthCard>
  );
}
