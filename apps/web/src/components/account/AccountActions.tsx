"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { postJson } from "@/lib/account/client";
import { problemMessage } from "@/lib/account/problems";
import { FormAlert } from "./FormParts";

type Texts = Dictionary["account"];

export function LogoutButton({ locale, texts }: { locale: string; texts: Texts }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);

  async function logout() {
    setBusy(true);
    await postJson("/api/v1/account/logout");
    router.replace(`/${locale}`);
    router.refresh();
  }

  return (
    <button type="button" onClick={logout} disabled={busy} className="h-11 rounded-xl border border-border px-4 font-medium hover:bg-background disabled:opacity-60">
      {busy ? texts.working : texts.logout}
    </button>
  );
}

export function ResendConfirmationButton({ texts }: { texts: Texts }) {
  const [state, setState] = useState<"idle" | "busy" | "sent" | { error: string }>("idle");

  async function resend() {
    setState("busy");
    const result = await postJson("/api/v1/account/resend-confirmation");
    setState(result.ok ? "sent" : { error: problemMessage(result.problem, texts.errors) });
  }

  if (state === "sent") return <FormAlert kind="success">{texts.resendSent}</FormAlert>;
  return (
    <div className="flex flex-col gap-2">
      {typeof state === "object" && <FormAlert kind="error">{state.error}</FormAlert>}
      <button type="button" onClick={resend} disabled={state === "busy"} className="self-start text-sm text-primary underline disabled:opacity-60">
        {state === "busy" ? texts.working : texts.resendConfirmation}
      </button>
    </div>
  );
}
