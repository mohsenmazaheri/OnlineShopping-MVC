import { redirect } from "next/navigation";
import { Suspense } from "react";
import { LogoutButton, ResendConfirmationButton } from "@/components/account/AccountActions";
import { AuthCard, FormAlert } from "@/components/account/FormParts";
import { providerNames } from "@/components/account/ExternalLoginButtons";
import { LinkedAccounts } from "@/components/account/LinkedAccounts";
import { PhoneCodeForm } from "@/components/account/PhoneCodeForm";
import { format } from "@/lib/account/digits";
import { accountMetadata, firstParam, routeLocale } from "@/lib/account/page-helpers";
import { messageFor } from "@/lib/account/problems";
import { getCurrentUser, getExternalLogins } from "@/lib/account/session";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";
import { loadStore } from "@/lib/store/load";

export async function generateMetadata({ params }: PageProps<"/[locale]/account">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.accountTitle);
}

type Search = Promise<Record<string, string | string[] | undefined>>;

/** Who is signed in, email confirmation, adding a phone, sign out. Profile, addresses and security follow in 2e. */
async function AccountSummary({ locale, texts, searchParams }: { locale: Locale; texts: Dictionary["account"]; searchParams: Search }) {
  const [user, store, query, external] = await Promise.all([getCurrentUser(), loadStore(), searchParams, getExternalLogins()]);
  const linkedProvider = firstParam(query.linked);
  const error = firstParam(query.error);
  // proxy.ts already redirects visitors without a session cookie; this covers expired or invalid sessions.
  if (!user) redirect(`/${locale}/account/login?returnUrl=${encodeURIComponent(`/${locale}/account`)}`);

  return (
    <div className="flex flex-col gap-4">
      {firstParam(query.welcome) === "1" && <FormAlert kind="success">{texts.welcome}</FormAlert>}
      {linkedProvider && (
        <FormAlert kind="success">{format(texts.linkedOk, { provider: providerNames[linkedProvider] ?? linkedProvider })}</FormAlert>
      )}
      {error && <FormAlert kind="error">{messageFor(error, texts.errors)}</FormAlert>}
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
      {!user.phoneNumber && store.signIn.phoneCode && (
        <section aria-labelledby="add-phone" className="rounded-xl border border-border p-3">
          <h2 id="add-phone" className="mb-3 font-semibold">
            {texts.addPhoneTitle}
          </h2>
          <PhoneCodeForm locale={locale} texts={texts} mode="addPhone" />
        </section>
      )}
      {external && (
        <LinkedAccounts locale={locale} texts={texts} available={external.available} linked={external.linked} />
      )}
      <LogoutButton locale={locale} texts={texts} />
    </div>
  );
}

export default async function AccountPage({ params, searchParams }: PageProps<"/[locale]/account">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.accountTitle}>
      <Suspense fallback={<p role="status">{dict.account.working}</p>}>
        <AccountSummary locale={locale} texts={dict.account} searchParams={searchParams} />
      </Suspense>
    </AuthCard>
  );
}
