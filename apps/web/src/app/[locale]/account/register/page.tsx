import { Suspense } from "react";
import { AuthCard } from "@/components/account/FormParts";
import { RegisterForm } from "@/components/account/RegisterForm";
import { accountMetadata, routeLocale } from "@/lib/account/page-helpers";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";
import { loadStore } from "@/lib/store/load";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/register">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.registerTitle);
}

/** The sign-up methods depend on this shop's settings (phone only with SMS), read per request. */
async function Register({ locale, texts }: { locale: Locale; texts: Dictionary["account"] }) {
  const store = await loadStore();
  return <RegisterForm locale={locale} texts={texts} signUp={store.signUp} />;
}

export default async function RegisterPage({ params }: PageProps<"/[locale]/account/register">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.registerTitle}>
      <Suspense fallback={null}>
        <Register locale={locale} texts={dict.account} />
      </Suspense>
    </AuthCard>
  );
}
