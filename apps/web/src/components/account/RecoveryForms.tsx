"use client";

import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { postJson } from "@/lib/account/client";
import { problemMessage } from "@/lib/account/problems";
import { FormAlert, FormField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";

type Texts = Dictionary["account"];

/** Asks for a reset link. The answer is the same whether or not the email has an account. */
export function ForgotPasswordForm({ locale, texts }: { locale: string; texts: Texts }) {
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [sent, setSent] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const email = String(new FormData(event.currentTarget).get("email") ?? "").trim();
    const result = await submit("/api/v1/account/forgot-password", { email, locale });
    if (result.ok) setSent(true);
  }

  if (sent) return <FormAlert kind="success">{texts.forgotSent}</FormAlert>;

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      <FormField label={texts.email} name="email" type="email" autoComplete="email" dir="ltr" required error={fieldError("email")} />
      <SubmitButton busy={busy} label={texts.submitForgot} busyLabel={texts.working} />
    </form>
  );
}

/** Sets a new password with the userId/token from the emailed link. */
export function ResetPasswordForm({ locale, texts, userId, token }: { locale: string; texts: Texts; userId: string; token: string }) {
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [done, setDone] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const newPassword = String(new FormData(event.currentTarget).get("newPassword") ?? "");
    const result = await submit("/api/v1/account/reset-password", { userId, token, newPassword });
    if (result.ok) setDone(true);
  }

  if (!userId || !token) return <FormAlert kind="error">{texts.errors.token_required}</FormAlert>;

  if (done) {
    return (
      <div className="flex flex-col gap-4">
        <FormAlert kind="success">{texts.resetDone}</FormAlert>
        <Link href={`/${locale}/account/login`} className="text-primary underline">
          {texts.submitLogin}
        </Link>
      </div>
    );
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      <FormField
        label={texts.newPassword}
        name="newPassword"
        type="password"
        autoComplete="new-password"
        required
        minLength={12}
        hint={texts.passwordHint}
        error={fieldError("newPassword")}
      />
      <SubmitButton busy={busy} label={texts.submitReset} busyLabel={texts.working} />
    </form>
  );
}

/** Confirms the email address as soon as the emailed link is opened. */
export function ConfirmEmail({ locale, texts, userId, token }: { locale: string; texts: Texts; userId: string; token: string }) {
  const [state, setState] = useState<{ status: "working" | "ok" | "error"; message?: string }>({ status: "working" });
  const started = useRef(false);
  const linkComplete = Boolean(userId && token);

  useEffect(() => {
    if (!linkComplete || started.current) return; // React may run effects twice in development.
    started.current = true;
    postJson("/api/v1/account/confirm-email", { userId, token }).then((result) =>
      setState(result.ok ? { status: "ok" } : { status: "error", message: problemMessage(result.problem, texts.errors) }),
    );
  }, [linkComplete, userId, token, texts]);

  if (!linkComplete) return <FormAlert kind="error">{texts.errors.token_required}</FormAlert>;
  if (state.status === "working") return <p role="status">{texts.confirming}</p>;
  if (state.status === "error") return <FormAlert kind="error">{state.message}</FormAlert>;
  return (
    <div className="flex flex-col gap-4">
      <FormAlert kind="success">{texts.confirmOk}</FormAlert>
      <Link href={`/${locale}`} className="text-primary underline">
        {texts.accountTitle}
      </Link>
    </div>
  );
}
