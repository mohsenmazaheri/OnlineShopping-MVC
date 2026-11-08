import { AuthCard } from "@/components/account/FormParts";
import { ForgotPasswordForm } from "@/components/account/RecoveryForms";
import { accountMetadata, routeLocale } from "@/lib/account/page-helpers";
import { getDictionary } from "@/lib/i18n/dictionaries";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/forgot-password">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.forgotTitle);
}

export default async function ForgotPasswordPage({ params }: PageProps<"/[locale]/account/forgot-password">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.forgotTitle}>
      <ForgotPasswordForm locale={locale} texts={dict.account} />
    </AuthCard>
  );
}
