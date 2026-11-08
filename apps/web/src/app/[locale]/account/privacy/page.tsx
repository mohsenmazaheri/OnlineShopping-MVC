import { redirect } from "next/navigation";
import { Suspense } from "react";
import { AccountNav } from "@/components/account/AccountNav";
import { DeleteAccountForm } from "@/components/account/DeleteAccountForm";
import { AuthCard } from "@/components/account/FormParts";
import { accountMetadata, routeLocale } from "@/lib/account/page-helpers";
import { getCurrentUser } from "@/lib/account/session";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/privacy">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.privacyTitle);
}

/** Download everything stored about the customer, and delete the account. */
async function Privacy({ locale, texts }: { locale: Locale; texts: Dictionary["account"] }) {
  const user = await getCurrentUser();
  if (!user) redirect(`/${locale}/account/login?returnUrl=${encodeURIComponent(`/${locale}/account/privacy`)}`);

  return (
    <div className="flex flex-col gap-4">
      <section aria-labelledby="download-data" className="rounded-xl border border-border p-3">
        <h2 id="download-data" className="mb-2 font-semibold">
          {texts.downloadDataTitle}
        </h2>
        <p className="mb-3 text-sm text-muted">{texts.downloadDataHelp}</p>
        {/* A plain link: the browser sends the session cookie and saves the file (Content-Disposition). */}
        <a
          href="/api/v1/account/personal-data"
          download
          className="inline-flex h-11 items-center rounded-xl border border-border px-4 font-medium hover:bg-background"
        >
          {texts.downloadData}
        </a>
      </section>
      {!user.isStaff && (
        <section aria-labelledby="delete-account" className="rounded-xl border border-red-200 p-3 dark:border-red-900">
          <h2 id="delete-account" className="mb-2 font-semibold text-red-700 dark:text-red-400">
            {texts.deleteAccountTitle}
          </h2>
          <DeleteAccountForm locale={locale} texts={texts} hasPassword={user.hasPassword} />
        </section>
      )}
    </div>
  );
}

export default async function PrivacyPage({ params }: PageProps<"/[locale]/account/privacy">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.privacyTitle}>
      <AccountNav locale={locale} texts={dict.account} current="privacy" />
      <Suspense fallback={<p role="status">{dict.account.working}</p>}>
        <Privacy locale={locale} texts={dict.account} />
      </Suspense>
    </AuthCard>
  );
}
