"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import type { FormEvent } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { FormAlert, FormField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";

type Props = {
  locale: string;
  texts: Dictionary["account"];
  /** Which identifiers this store accepts at sign-up (phone only with SMS configured). */
  signUp: { email: boolean; phone: boolean };
};

export function RegisterForm({ locale, texts, signUp }: Props) {
  const router = useRouter();
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const value = (name: string) => {
      const v = String(form.get(name) ?? "").trim();
      return v.length > 0 ? v : null;
    };
    const result = await submit("/api/v1/account/register", {
      email: signUp.email ? value("email") : null,
      phone: signUp.phone ? value("phone") : null,
      password: String(form.get("password") ?? ""),
      displayName: value("displayName"),
      locale,
    });
    if (result.ok) {
      router.replace(`/${locale}/account`);
      router.refresh();
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      <FormField label={texts.displayName} name="displayName" autoComplete="name" maxLength={100} error={fieldError("displayName")} />
      {signUp.email && (
        <FormField label={texts.email} name="email" type="email" autoComplete="email" dir="ltr" error={fieldError("email")} />
      )}
      {signUp.phone && (
        <FormField label={texts.phone} name="phone" type="tel" autoComplete="tel" inputMode="tel" dir="ltr" error={fieldError("phone")} />
      )}
      <FormField
        label={texts.password}
        name="password"
        type="password"
        autoComplete="new-password"
        required
        minLength={12}
        hint={texts.passwordHint}
        error={fieldError("password")}
      />
      <SubmitButton busy={busy} label={texts.submitRegister} busyLabel={texts.working} />
      <p className="text-sm">
        {texts.haveAccount}{" "}
        <Link href={`/${locale}/account/login`} className="text-primary underline">
          {texts.submitLogin}
        </Link>
      </p>
    </form>
  );
}
