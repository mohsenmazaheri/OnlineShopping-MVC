import { Suspense } from "react";
import { AuthCard } from "@/components/account/FormParts";
import { LoginForm } from "@/components/account/LoginForm";
import { accountMetadata, firstParam, routeLocale } from "@/lib/account/page-helpers";
import { safeReturnPath } from "@/lib/account/return-path";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/login">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.loginTitle);
}

async function Login({ locale, texts, searchParams }: { locale: Locale; texts: Dictionary["account"]; searchParams: Promise<Record<string, string | string[] | undefined>> }) {
  const returnTo = safeReturnPath(firstParam((await searchParams).returnUrl), locale);
  return <LoginForm locale={locale} texts={texts} returnTo={returnTo} />;
}

export default async function LoginPage({ params, searchParams }: PageProps<"/[locale]/account/login">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.loginTitle}>
      <Suspense fallback={null}>
        <Login locale={locale} texts={dict.account} searchParams={searchParams} />
      </Suspense>
    </AuthCard>
  );
}
