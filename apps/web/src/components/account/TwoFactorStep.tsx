"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { cleanCode } from "@/lib/account/digits";
import { FormAlert, FormField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";

type Props = { texts: Dictionary["account"]; returnTo: string };

/** Second sign-in step after a password, SMS code or provider: authenticator code, or a one-time recovery code. */
export function TwoFactorStep({ texts, returnTo }: Props) {
  const router = useRouter();
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [useRecovery, setUseRecovery] = useState(false);
  const [code, setCode] = useState("");

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const result = useRecovery
      ? await submit("/api/v1/account/2fa/recovery", { recoveryCode: String(form.get("recoveryCode") ?? "").trim() })
      : await submit("/api/v1/account/2fa/verify", { code, rememberMe: true, rememberDevice: form.get("rememberDevice") === "on" });
    if (result.ok) {
      router.replace(returnTo);
      router.refresh();
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      <h2 className="font-semibold">{texts.twoFactorTitle}</h2>
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      {useRecovery ? (
        <FormField label={texts.recoveryCode} name="recoveryCode" autoComplete="off" dir="ltr" required autoFocus error={fieldError("recoveryCode")} />
      ) : (
        <>
          <p className="text-sm text-muted">{texts.twoFactorPrompt}</p>
          <FormField
            label={texts.authenticatorCode}
            name="code"
            autoComplete="one-time-code"
            inputMode="numeric"
            dir="ltr"
            maxLength={6}
            required
            autoFocus
            value={code}
            onChange={(e) => setCode(cleanCode(e.target.value))}
            className="text-center text-xl tracking-[0.5em]"
            error={fieldError("code")}
          />
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" name="rememberDevice" className="h-4 w-4 accent-[var(--primary)]" />
            {texts.rememberDevice}
          </label>
        </>
      )}
      <SubmitButton busy={busy} label={texts.verify} busyLabel={texts.working} />
      <button type="button" className="self-start text-sm text-primary underline" onClick={() => setUseRecovery((v) => !v)}>
        {useRecovery ? texts.useAuthenticator : texts.useRecoveryCode}
      </button>
    </form>
  );
}
