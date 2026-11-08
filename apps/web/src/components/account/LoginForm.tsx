"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import type { FormEvent } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { FormAlert, FormField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";

type Props = { locale: string; texts: Dictionary["account"]; returnTo: string };

export function LoginForm({ locale, texts, returnTo }: Props) {
  const router = useRouter();
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const result = await submit("/api/v1/account/login", {
      identifier: String(form.get("identifier") ?? ""),
      password: String(form.get("password") ?? ""),
      rememberMe: form.get("rememberMe") === "on",
    });
    if (result.ok) {
      router.replace(returnTo);
      router.refresh();
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      <FormField label={texts.identifier} name="identifier" autoComplete="username" inputMode="email" required error={fieldError("identifier")} />
      <FormField label={texts.password} name="password" type="password" autoComplete="current-password" required error={fieldError("password")} />
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" name="rememberMe" className="h-4 w-4 accent-[var(--primary)]" />
        {texts.rememberMe}
      </label>
      <SubmitButton busy={busy} label={texts.submitLogin} busyLabel={texts.working} />
      <div className="flex flex-wrap justify-between gap-2 text-sm">
        <Link href={`/${locale}/account/forgot-password`} className="text-primary underline">
          {texts.forgotLink}
        </Link>
        <span>
          {texts.noAccount}{" "}
          <Link href={`/${locale}/account/register`} className="text-primary underline">
            {texts.submitRegister}
          </Link>
        </span>
      </div>
    </form>
  );
}
