"use client";

import { useRouter } from "next/navigation";
import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { cleanCode, format, toAsciiDigits } from "@/lib/account/digits";
import { FormAlert, FormField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";
import { useCountdown } from "./useCountdown";

type Texts = Dictionary["account"];
type Sent = { expiresInSeconds: number; resendAfterSeconds: number };

type Props = {
  locale: string;
  texts: Texts;
  /**
   * signIn: sign in (or create the account) with a code, then go to returnTo.
   * addPhone: the signed-in user verifies a number for their account.
   */
  mode: "signIn" | "addPhone";
  returnTo?: string;
};

/** WebOTP (Chrome on Android): reads the code from the incoming SMS ("@domain #code") and fills it in. */
type OtpCredential = Credential & { code: string };

export function PhoneCodeForm({ locale, texts, mode, returnTo }: Props) {
  const router = useRouter();
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [phone, setPhone] = useState("");
  const [step, setStep] = useState<"phone" | "code" | "done">("phone");
  const [code, setCode] = useState("");
  const [resendAt, setResendAt] = useState<number | null>(null);
  const secondsLeft = useCountdown(resendAt);
  const formRef = useRef<HTMLFormElement>(null);

  const paths =
    mode === "signIn"
      ? { send: "/api/v1/account/phone/send-code", verify: "/api/v1/account/phone/sign-in" }
      : { send: "/api/v1/account/phone/add/send-code", verify: "/api/v1/account/phone/add/confirm" };

  async function sendCode() {
    const result = await submit<Sent>(paths.send as `/api/v1/${string}`, { phone: toAsciiDigits(phone), locale });
    if (result.ok) {
      setStep("code");
      setCode("");
      setResendAt(Date.now() + result.data.resendAfterSeconds * 1000);
    }
  }

  async function verify() {
    const body =
      mode === "signIn"
        ? { phone: toAsciiDigits(phone), code, displayName: null, locale, rememberMe: true }
        : { phone: toAsciiDigits(phone), code };
    const result = await submit<{ isNewAccount?: boolean }>(paths.verify as `/api/v1/${string}`, body);
    if (!result.ok) return;
    if (mode === "addPhone") {
      setStep("done");
      router.refresh();
      return;
    }
    router.replace(result.data?.isNewAccount ? `/${locale}/account?welcome=1` : (returnTo ?? `/${locale}`));
    router.refresh();
  }

  // Progressive enhancement: on supporting phones the code fills itself in from the SMS.
  useEffect(() => {
    if (step !== "code" || typeof window === "undefined" || !("OTPCredential" in window)) return;
    const abort = new AbortController();
    navigator.credentials
      .get({ otp: { transport: ["sms"] }, signal: abort.signal } as CredentialRequestOptions)
      .then((credential) => {
        const otp = credential as OtpCredential | null;
        if (otp?.code) {
          setCode(cleanCode(otp.code));
          formRef.current?.requestSubmit();
        }
      })
      .catch(() => {
        // User dismissed it, or the browser cannot read SMS: typing the code still works.
      });
    return () => abort.abort();
  }, [step]);

  function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    void (step === "phone" ? sendCode() : verify());
  }

  if (step === "done") return <FormAlert kind="success">{texts.phoneSaved}</FormAlert>;

  return (
    <form ref={formRef} onSubmit={onSubmit} noValidate className="flex flex-col gap-4">
      {formError && <FormAlert kind="error">{formError}</FormAlert>}

      {step === "phone" ? (
        <>
          <FormField
            label={texts.phone}
            name="phone"
            type="tel"
            autoComplete="tel"
            inputMode="tel"
            dir="ltr"
            required
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            hint={texts.phoneHint}
            error={fieldError("phone")}
          />
          <SubmitButton busy={busy} label={texts.sendCode} busyLabel={texts.working} />
        </>
      ) : (
        <>
          <p className="text-sm text-muted">{format(texts.codeSentTo, { phone: toAsciiDigits(phone) })}</p>
          <FormField
            label={texts.code}
            name="code"
            autoComplete="one-time-code"
            inputMode="numeric"
            pattern="[0-9]*"
            dir="ltr"
            maxLength={6}
            required
            autoFocus
            value={code}
            onChange={(e) => setCode(cleanCode(e.target.value))}
            className="text-center text-xl tracking-[0.5em]"
            error={fieldError("code")}
          />
          <SubmitButton busy={busy} label={mode === "signIn" ? texts.verifyCode : texts.addPhoneSave} busyLabel={texts.working} />
          <div className="flex flex-wrap justify-between gap-2 text-sm">
            <button type="button" className="text-primary underline" onClick={() => setStep("phone")}>
              {texts.changeNumber}
            </button>
            {secondsLeft > 0 ? (
              <span className="text-muted" aria-live="polite">
                {format(texts.resendIn, { seconds: secondsLeft })}
              </span>
            ) : (
              <button type="button" className="text-primary underline" disabled={busy} onClick={() => void sendCode()}>
                {texts.resend}
              </button>
            )}
          </div>
        </>
      )}
    </form>
  );
}
