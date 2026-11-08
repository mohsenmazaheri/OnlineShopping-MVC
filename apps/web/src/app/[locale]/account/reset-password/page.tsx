import { Suspense } from "react";
import { AuthCard } from "@/components/account/FormParts";
import { ResetPasswordForm } from "@/components/account/RecoveryForms";
import { accountMetadata, firstParam, routeLocale } from "@/lib/account/page-helpers";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/reset-password">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return { ...accountMetadata(dict.account.resetTitle), referrer: "no-referrer" as const };
}

async function Reset({ locale, texts, searchParams }: { locale: Locale; texts: Dictionary["account"]; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const query = await searchParams;
  return <ResetPasswordForm locale={locale} texts={texts} userId={firstParam(query.userId)} token={firstParam(query.token)} />;
}

export default async function ResetPasswordPage({ params, searchParams }: PageProps<"/[locale]/account/reset-password">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.resetTitle}>
      <Suspense fallback={null}>
        <Reset locale={locale} texts={dict.account} searchParams={searchParams} />
      </Suspense>
    </AuthCard>
  );
}
