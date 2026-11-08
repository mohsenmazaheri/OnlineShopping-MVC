import Link from "next/link";
import type { Dictionary } from "@/lib/i18n/dictionaries";

export type AccountSection = "profile" | "addresses" | "privacy";

/** Tabs between the account pages (plain links: works without JavaScript). */
export function AccountNav({ locale, texts, current }: { locale: string; texts: Dictionary["account"]; current: AccountSection }) {
  const items: { section: AccountSection; href: string; label: string }[] = [
    { section: "profile", href: `/${locale}/account`, label: texts.navProfile },
    { section: "addresses", href: `/${locale}/account/addresses`, label: texts.navAddresses },
    { section: "privacy", href: `/${locale}/account/privacy`, label: texts.navPrivacy },
  ];
  return (
    <nav aria-label={texts.accountTitle} className="mb-5 flex gap-1 rounded-xl bg-background p-1 text-sm">
      {items.map((item) => (
        <Link
          key={item.section}
          href={item.href}
          aria-current={item.section === current ? "page" : undefined}
          className={`flex h-10 flex-1 items-center justify-center rounded-lg font-medium transition ${
            item.section === current ? "bg-surface shadow-sm" : "text-muted hover:text-foreground"
          }`}
        >
          {item.label}
        </Link>
      ))}
    </nav>
  );
}
