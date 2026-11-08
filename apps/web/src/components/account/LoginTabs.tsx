"use client";

import { useState } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { LoginForm } from "./LoginForm";
import { PhoneCodeForm } from "./PhoneCodeForm";

type Method = "code" | "password";

type Props = {
  locale: string;
  texts: Dictionary["account"];
  returnTo: string;
  /** SMS codes are offered only when the shop has an SMS provider. */
  phoneCode: boolean;
  initial?: Method;
};

/** Sign-in by SMS code (when available, shown first: it also creates the account) or by password. */
export function LoginTabs({ locale, texts, returnTo, phoneCode, initial }: Props) {
  const [method, setMethod] = useState<Method>(phoneCode ? (initial ?? "code") : "password");

  if (!phoneCode) return <LoginForm locale={locale} texts={texts} returnTo={returnTo} />;

  const tab = (value: Method, label: string) => (
    <button
      type="button"
      role="tab"
      aria-selected={method === value}
      onClick={() => setMethod(value)}
      className={`h-10 flex-1 rounded-lg text-sm font-medium transition ${
        method === value ? "bg-surface shadow-sm" : "text-muted hover:text-foreground"
      }`}
    >
      {label}
    </button>
  );

  return (
    <div className="flex flex-col gap-5">
      <div role="tablist" className="flex gap-1 rounded-xl bg-background p-1">
        {tab("code", texts.withCode)}
        {tab("password", texts.withPassword)}
      </div>
      <div role="tabpanel">
        {method === "code" ? (
          <PhoneCodeForm locale={locale} texts={texts} mode="signIn" returnTo={returnTo} />
        ) : (
          <LoginForm locale={locale} texts={texts} returnTo={returnTo} />
        )}
      </div>
    </div>
  );
}
