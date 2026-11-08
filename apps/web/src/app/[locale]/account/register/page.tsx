import Link from "next/link";
import { Suspense } from "react";
import { ExternalLoginButtons } from "@/components/account/ExternalLoginButtons";
import { AuthCard } from "@/components/account/FormParts";
import { PhoneCodeForm } from "@/components/account/PhoneCodeForm";
import { RegisterForm } from "@/components/account/RegisterForm";
import { accountMetadata, routeLocale } from "@/lib/account/page-helpers";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";
import { loadStore } from "@/lib/store/load";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/register">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.registerTitle);
}

/**
 * Email + password sign-up when enabled; with SMS, a mobile number signs up through a code (the account is created
 * on first sign-in). A shop with phone-only sign-up shows just the code form.
 */
async function Register({ locale, texts }: { locale: Locale; texts: Dictionary["account"] }) {
  const store = await loadStore();
  const phone = store.signIn.phoneCode && store.signUp.phone;

  if (!store.signUp.email) {
    return phone ? <PhoneCodeForm locale={locale} texts={texts} mode="signIn" /> : <p>{texts.errors.email_sign_up_disabled}</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      <RegisterForm locale={locale} texts={texts} />
      {phone && (
        <Link href={`/${locale}/account/login`} className="text-center text-sm text-primary underline">
          {texts.registerWithPhone}
        </Link>
      )}
      {/* A provider sign-in creates the account on first use. */}
      <ExternalLoginButtons providers={store.signIn.external} locale={locale} returnTo={`/${locale}/account`} texts={texts} />
    </div>
  );
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
