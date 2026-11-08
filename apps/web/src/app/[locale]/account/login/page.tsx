import { Suspense } from "react";
import { AuthCard } from "@/components/account/FormParts";
import { LoginTabs } from "@/components/account/LoginTabs";
import { accountMetadata, firstParam, routeLocale } from "@/lib/account/page-helpers";
import { safeReturnPath } from "@/lib/account/return-path";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";
import { loadStore } from "@/lib/store/load";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/login">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.loginTitle);
}

type Search = Promise<Record<string, string | string[] | undefined>>;

/** Reads the return address and this shop's sign-in methods (SMS codes only with an SMS provider) per request. */
async function Login({ locale, texts, searchParams }: { locale: Locale; texts: Dictionary["account"]; searchParams: Search }) {
  const [query, store] = await Promise.all([searchParams, loadStore()]);
  const returnTo = safeReturnPath(firstParam(query.returnUrl), locale);
  const initial = firstParam(query.method) === "password" ? "password" : undefined;
  return <LoginTabs locale={locale} texts={texts} returnTo={returnTo} phoneCode={store.signIn.phoneCode} initial={initial} />;
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
