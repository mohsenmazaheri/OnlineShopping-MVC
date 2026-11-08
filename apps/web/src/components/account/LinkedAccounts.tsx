"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { postJson } from "@/lib/account/client";
import { problemMessage } from "@/lib/account/problems";
import { FormAlert } from "./FormParts";
import { providerNames } from "./ExternalLoginButtons";

type Props = { locale: string; texts: Dictionary["account"]; available: string[]; linked: string[] };

/** Link (full-page round trip through the provider) or unlink each provider the shop offers. */
export function LinkedAccounts({ locale, texts, available, linked }: Props) {
  const router = useRouter();
  const [busy, setBusy] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  async function unlink(provider: string) {
    setBusy(provider);
    setError(null);
    const result = await postJson(`/api/v1/account/external/${provider}/unlink`);
    setBusy(null);
    if (result.ok) router.refresh();
    else setError(problemMessage(result.problem, texts.errors));
  }

  if (available.length === 0) return null;

  return (
    <section aria-labelledby="linked-accounts" className="rounded-xl border border-border p-3">
      <h2 id="linked-accounts" className="mb-3 font-semibold">
        {texts.linkedAccounts}
      </h2>
      {error && <FormAlert kind="error">{error}</FormAlert>}
      <ul className="mt-2 flex flex-col gap-2">
        {available.map((provider) => (
          <li key={provider} className="flex items-center justify-between gap-3">
            <span>{providerNames[provider] ?? provider}</span>
            {linked.includes(provider) ? (
              <button
                type="button"
                onClick={() => unlink(provider)}
                disabled={busy !== null}
                className="text-sm text-red-600 underline disabled:opacity-60 dark:text-red-400"
              >
                {busy === provider ? texts.working : texts.unlink}
              </button>
            ) : (
              <a href={`/api/v1/account/external/${provider}/link?locale=${encodeURIComponent(locale)}`} className="text-sm text-primary underline">
                {texts.link}
              </a>
            )}
          </li>
        ))}
      </ul>
    </section>
  );
}
