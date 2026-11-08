"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { nativeLanguageName } from "@/lib/i18n/display-names";
import { FormAlert, FormField, SelectField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";

type Texts = Dictionary["account"];

type ProfileProps = {
  texts: Texts;
  displayName: string | null;
  preferredLocale: string | null;
  /** Languages the shop offers (the language picker shows only these). */
  locales: readonly string[];
};

/** Display name and the language of emails/SMS. */
export function ProfileForm({ texts, displayName, preferredLocale, locales }: ProfileProps) {
  const router = useRouter();
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [saved, setSaved] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setSaved(false);
    const form = new FormData(event.currentTarget);
    const result = await submit(
      "/api/v1/account/profile",
      { displayName: String(form.get("displayName") ?? ""), preferredLocale: String(form.get("preferredLocale") ?? "") },
      "PUT",
    );
    if (result.ok) {
      setSaved(true);
      router.refresh();
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-3">
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      {saved && <FormAlert kind="success">{texts.saved}</FormAlert>}
      <FormField label={texts.displayName} name="displayName" autoComplete="name" maxLength={100} defaultValue={displayName ?? ""} error={fieldError("displayName")} />
      <SelectField
        label={texts.preferredLanguage}
        name="preferredLocale"
        defaultValue={preferredLocale && locales.includes(preferredLocale) ? preferredLocale : locales[0]}
        options={locales.map((l) => ({ value: l, label: nativeLanguageName(l) }))}
        error={fieldError("preferredLocale")}
      />
      <SubmitButton busy={busy} label={texts.save} busyLabel={texts.working} />
    </form>
  );
}

/** Current password + new password; other devices are signed out by the API. */
export function ChangePasswordForm({ texts }: { texts: Texts }) {
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);
  const [done, setDone] = useState(false);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setDone(false);
    const formElement = event.currentTarget;
    const form = new FormData(formElement);
    const result = await submit("/api/v1/account/change-password", {
      currentPassword: String(form.get("currentPassword") ?? ""),
      newPassword: String(form.get("newPassword") ?? ""),
    });
    if (result.ok) {
      setDone(true);
      formElement.reset();
    }
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-3">
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      {done && <FormAlert kind="success">{texts.passwordChanged}</FormAlert>}
      <FormField label={texts.currentPassword} name="currentPassword" type="password" autoComplete="current-password" required error={fieldError("currentPassword")} />
      <FormField
        label={texts.newPassword}
        name="newPassword"
        type="password"
        autoComplete="new-password"
        required
        hint={texts.passwordHint}
        error={fieldError("newPassword")}
      />
      <SubmitButton busy={busy} label={texts.changePassword} busyLabel={texts.working} />
    </form>
  );
}
