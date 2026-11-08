"use client";

import QRCode from "qrcode";
import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { cleanCode, format } from "@/lib/account/digits";
import { FormAlert, FormField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";

type Texts = Dictionary["account"];
type Status = { enabled: boolean; recoveryCodesLeft: number | string; rememberedDevice: boolean; requiredForAccount: boolean };
type Setup = { sharedKey: string; authenticatorUri: string };
type Mode = { kind: "idle" } | { kind: "setup"; setup: Setup; qr: string } | { kind: "codes"; codes: string[] } | { kind: "confirm"; action: "disable" | "codes" };

/** Turn two-step sign-in on (QR code + first code), show recovery codes once, renew them, or turn it off. */
export function TwoFactorSettings({ texts, status }: { texts: Texts; status: Status }) {
  const router = useRouter();
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [mode, setMode] = useState<Mode>({ kind: "idle" });
  const [code, setCode] = useState("");

  async function beginSetup() {
    const result = await submit<Setup>("/api/v1/account/2fa/setup", undefined);
    if (!result.ok) return;
    // Generated in the browser from the otpauth:// URI: the secret never goes to a third-party QR service.
    const qr = await QRCode.toDataURL(result.data.authenticatorUri, { margin: 1, width: 200 });
    setCode("");
    setMode({ kind: "setup", setup: result.data, qr });
  }

  async function onConfirm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (mode.kind === "setup") {
      const result = await submit<{ codes: string[] }>("/api/v1/account/2fa/enable", { code });
      if (result.ok) setMode({ kind: "codes", codes: result.data.codes });
    } else if (mode.kind === "confirm" && mode.action === "codes") {
      const result = await submit<{ codes: string[] }>("/api/v1/account/2fa/recovery-codes", { code });
      if (result.ok) setMode({ kind: "codes", codes: result.data.codes });
    } else if (mode.kind === "confirm" && mode.action === "disable") {
      const result = await submit("/api/v1/account/2fa/disable", { code });
      if (result.ok) {
        setMode({ kind: "idle" });
        router.refresh();
      }
    }
  }

  function downloadCodes(codes: string[]) {
    const url = URL.createObjectURL(new Blob([codes.join("\n") + "\n"], { type: "text/plain" }));
    const link = Object.assign(document.createElement("a"), { href: url, download: "recovery-codes.txt" });
    link.click();
    URL.revokeObjectURL(url);
  }

  const codeField = (
    <FormField
      label={texts.authenticatorCode}
      name="code"
      autoComplete="one-time-code"
      inputMode="numeric"
      dir="ltr"
      maxLength={6}
      required
      value={code}
      onChange={(e) => setCode(cleanCode(e.target.value))}
      className="text-center text-xl tracking-[0.5em]"
      error={fieldError("code")}
    />
  );

  return (
    <section aria-labelledby="two-factor" className="rounded-xl border border-border p-3">
      <h2 id="two-factor" className="mb-2 font-semibold">
        {texts.twoFactorTitle}
      </h2>
      {formError && <FormAlert kind="error">{formError}</FormAlert>}

      {mode.kind === "codes" ? (
        <div className="flex flex-col gap-3">
          <p className="text-sm font-medium">{texts.recoveryCodesTitle}</p>
          <p className="text-sm text-muted">{texts.recoveryCodesHelp}</p>
          <ul dir="ltr" className="grid grid-cols-2 gap-1 rounded-lg bg-background p-3 font-mono text-sm">
            {mode.codes.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
          <div className="flex gap-3">
            <button type="button" className="text-sm text-primary underline" onClick={() => downloadCodes(mode.codes)}>
              {texts.downloadCodes}
            </button>
            <button type="button" className="text-sm text-primary underline" onClick={() => { setMode({ kind: "idle" }); router.refresh(); }}>
              {texts.done}
            </button>
          </div>
        </div>
      ) : mode.kind === "setup" ? (
        <form onSubmit={onConfirm} noValidate className="flex flex-col gap-3">
          <p className="text-sm">{texts.setupScan}</p>
          {/* eslint-disable-next-line @next/next/no-img-element -- a generated data: URL, nothing to optimize */}
          <img src={mode.qr} alt="QR" width={200} height={200} className="self-center rounded-lg bg-white p-1" />
          <p className="text-sm">
            {texts.setupKey}: <code dir="ltr" className="select-all rounded bg-background px-1">{mode.setup.sharedKey}</code>
          </p>
          <p className="text-sm">{texts.setupConfirm}</p>
          {codeField}
          <SubmitButton busy={busy} label={texts.enable} busyLabel={texts.working} />
        </form>
      ) : mode.kind === "confirm" ? (
        <form onSubmit={onConfirm} noValidate className="flex flex-col gap-3">
          <p className="text-sm">{texts.codeToConfirm}</p>
          {codeField}
          <SubmitButton busy={busy} label={mode.action === "disable" ? texts.disableTwoFactor : texts.newRecoveryCodes} busyLabel={texts.working} />
        </form>
      ) : status.enabled ? (
        <div className="flex flex-col gap-2 text-sm">
          <p>{texts.twoFactorOn}</p>
          <p className="text-muted">{format(texts.recoveryCodesLeft, { count: status.recoveryCodesLeft })}</p>
          <div className="flex flex-wrap gap-3">
            <button type="button" className="text-primary underline" onClick={() => { setCode(""); setMode({ kind: "confirm", action: "codes" }); }}>
              {texts.newRecoveryCodes}
            </button>
            {status.rememberedDevice && (
              <button type="button" className="text-primary underline" onClick={async () => { await submit("/api/v1/account/2fa/forget-device", undefined); router.refresh(); }}>
                {texts.forgetDevice}
              </button>
            )}
            {!status.requiredForAccount && (
              <button type="button" className="text-red-600 underline dark:text-red-400" onClick={() => { setCode(""); setMode({ kind: "confirm", action: "disable" }); }}>
                {texts.disableTwoFactor}
              </button>
            )}
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-3 text-sm">
          <p className={status.requiredForAccount ? "font-medium text-amber-700 dark:text-amber-300" : "text-muted"}>
            {status.requiredForAccount ? texts.twoFactorRequired : texts.twoFactorOff}
          </p>
          <button type="button" disabled={busy} onClick={beginSetup} className="h-11 self-start rounded-xl bg-primary px-4 font-semibold text-primary-foreground disabled:opacity-60">
            {texts.enableTwoFactor}
          </button>
        </div>
      )}
    </section>
  );
}
