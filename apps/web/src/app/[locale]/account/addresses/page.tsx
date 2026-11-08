import { redirect } from "next/navigation";
import { Suspense } from "react";
import { AccountNav } from "@/components/account/AccountNav";
import { AddressBook } from "@/components/account/AddressBook";
import { AuthCard } from "@/components/account/FormParts";
import { accountMetadata, routeLocale } from "@/lib/account/page-helpers";
import { getAddresses } from "@/lib/account/session";
import type { Locale } from "@/lib/i18n/config";
import { getDictionary, type Dictionary } from "@/lib/i18n/dictionaries";
import { loadStore } from "@/lib/store/load";

export async function generateMetadata({ params }: PageProps<"/[locale]/account/addresses">) {
  const dict = await getDictionary(routeLocale((await params).locale));
  return accountMetadata(dict.account.addressesTitle);
}

/** The address book is per user: read at request time, never cached. */
async function Addresses({ locale, texts }: { locale: Locale; texts: Dictionary["account"] }) {
  const [addresses, store] = await Promise.all([getAddresses(), loadStore()]);
  // proxy.ts already redirects visitors without a session cookie; this covers expired sessions.
  if (!addresses) redirect(`/${locale}/account/login?returnUrl=${encodeURIComponent(`/${locale}/account/addresses`)}`);
  return <AddressBook locale={locale} texts={texts} addresses={addresses} countries={store.checkout.shippingCountries} />;
}

export default async function AddressesPage({ params }: PageProps<"/[locale]/account/addresses">) {
  const locale = routeLocale((await params).locale);
  const dict = await getDictionary(locale);
  return (
    <AuthCard title={dict.account.addressesTitle}>
      <AccountNav locale={locale} texts={dict.account} current="addresses" />
      <Suspense fallback={<p role="status">{dict.account.working}</p>}>
        <Addresses locale={locale} texts={dict.account} />
      </Suspense>
    </AuthCard>
  );
}
