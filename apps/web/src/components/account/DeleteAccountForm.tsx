"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import { postJson } from "@/lib/account/client";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { FormAlert } from "./FormParts";
import { PasswordField } from "./PasswordField";
import { useApiForm } from "./useApiForm";

const REAUTHENTICATION_REQUIRED = "account.reauthentication_required";

type Props = { locale: string; texts: Dictionary["account"]; hasPassword: boolean };

/**
 * Deletes the account after an explicit confirmation. Password accounts type their password; accounts without one
 * must have signed in recently, otherwise they are offered a fresh sign-in that comes back here.
 */
export function DeleteAccountForm({ locale, texts, hasPassword }: Props) {
  const router = useRouter();
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [confirmed, setConfirmed] = useState(false);
  const [needsSignIn, setNeedsSignIn] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const result = await submit("/api/v1/account/delete", { password: hasPassword ? String(form.get("password") ?? "") : null });
    if (result.ok) {
      router.replace(`/${locale}`);
      router.refresh();
    } else if (result.problem.code === REAUTHENTICATION_REQUIRED) {
      setNeedsSignIn(true);
    }
  }

  async function signInAgain() {
    await postJson("/api/v1/account/logout");
    router.replace(`/${locale}/account/login?returnUrl=${encodeURIComponent(`/${locale}/account/privacy`)}`);
    router.refresh();
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-3">
      <p className="text-sm text-muted">{texts.deleteAccountHelp}</p>
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      {needsSignIn && (
        <button type="button" onClick={signInAgain} className="h-11 self-start rounded-xl border border-border px-4 font-medium hover:bg-background">
          {texts.signInAgain}
        </button>
      )}
      {hasPassword ? (
        <PasswordField label={texts.password} name="password" autoComplete="current-password" required error={fieldError("password")} showLabel={texts.showPassword} hideLabel={texts.hidePassword} />
      ) : (
        <p className="text-sm">{texts.deleteAccountPasswordless}</p>
      )}
      <label className="flex items-center gap-2 text-sm">
        <input type="checkbox" checked={confirmed} onChange={(e) => setConfirmed(e.target.checked)} className="h-4 w-4 accent-red-600" />
        {texts.deleteAccountConfirm}
      </label>
      <button
        type="submit"
        disabled={!confirmed || busy}
        aria-busy={busy}
        className="h-12 rounded-xl bg-red-600 px-4 font-semibold text-white transition hover:bg-red-700 disabled:opacity-50"
      >
        {busy ? texts.working : texts.deleteAccount}
      </button>
    </form>
  );
}
