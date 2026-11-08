import { Suspense } from "react";
import { AuthCard } from "@/components/account/FormParts";
import { ConfirmEmail } from "@/components/account/RecoveryForms";
import { accountMetadata, firstParam, routeLocale } from "@/lib/account/page-helpers";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/confirm-email">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return { ...accountMetadata(dict.account.confirmTitle), referrer: "no-referrer" as const };
}

async function Confirm({ locale, texts, searchParams }: { locale: Locale; texts: Dictionary["account"]; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const query = await searchParams;
  return <ConfirmEmail locale={locale} texts={texts} userId={firstParam(query.userId)} token={firstParam(query.token)} />;
}

export default async function ConfirmEmailPage({ params, searchParams }: PageProps<"/[locale]/account/confirm-email">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.confirmTitle}>
      <Suspense fallback={<p role="status">{dict.account.confirming}</p>}>
        <Confirm locale={locale} texts={dict.account} searchParams={searchParams} />
      </Suspense>
    </AuthCard>
  );
}
