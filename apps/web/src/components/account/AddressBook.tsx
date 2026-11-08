"use client";

import { useRouter } from "next/navigation";
import { useState, type FormEvent } from "react";
import type { Address } from "@/lib/account/session";
import { directionOf, isLocale } from "@/lib/i18n/config";
import type { Dictionary } from "@/lib/i18n/dictionaries";
import { regionName } from "@/lib/i18n/display-names";
import { FormAlert, FormField, SelectField, SubmitButton } from "./FormParts";
import { useApiForm } from "./useApiForm";

type Texts = Dictionary["account"];

type Props = {
  locale: string;
  texts: Texts;
  /** The address book as the server rendered it (refreshed after every change). */
  addresses: readonly Address[];
  /** Countries the shop ships to (ISO codes). */
  countries: readonly string[];
};

/** List, add, edit, delete and pick the default address. The server is the source of truth: changes refresh the page. */
export function AddressBook({ locale, texts, addresses, countries }: Props) {
  const router = useRouter();
  const { busy, submit, formError } = useApiForm(texts.errors);
  const [editing, setEditing] = useState<"new" | string | null>(addresses.length === 0 ? "new" : null);

  async function act(path: `/api/v1/${string}`, method: "POST" | "DELETE") {
    const result = await submit(path, undefined, method);
    if (result.ok) router.refresh();
  }

  async function remove(address: Address) {
    if (window.confirm(texts.confirmDeleteAddress)) await act(`/api/v1/account/addresses/${address.id}`, "DELETE");
  }

  // Persian and Arabic use their own comma.
  const comma = isLocale(locale) && directionOf(locale) === "rtl" ? "، " : ", ";

  const done = () => {
    setEditing(null);
    router.refresh();
  };

  return (
    <div className="flex flex-col gap-3">
      {formError && editing === null && <FormAlert kind="error">{formError}</FormAlert>}
      {addresses.length === 0 && editing === null && <p className="text-sm text-muted">{texts.noAddresses}</p>}

      <ul className="flex flex-col gap-3">
        {addresses.map((address) =>
          editing === address.id ? (
            <li key={address.id}>
              <AddressForm locale={locale} texts={texts} countries={countries} address={address} onDone={done} onCancel={() => setEditing(null)} />
            </li>
          ) : (
            <li key={address.id} className="rounded-xl border border-border p-3 text-sm">
              <div className="mb-1 flex items-center gap-2">
                <span className="font-semibold">{address.label ?? address.recipientName}</span>
                {address.isDefault && <span className="rounded-full bg-primary/10 px-2 py-0.5 text-xs text-primary">{texts.defaultAddress}</span>}
              </div>
              <p>{address.recipientName}</p>
              <p>
                {[address.line1, address.line2].filter(Boolean).join(comma)}
              </p>
              <p>
                {[address.city, address.region, regionName(address.countryCode, locale)].filter(Boolean).join(comma)}
                {address.postalCode && (
                  <>
                    {" — "}
                    <bdi dir="ltr">{address.postalCode}</bdi>
                  </>
                )}
              </p>
              <p dir="ltr" className="text-start text-muted">
                {address.phoneNumber}
              </p>
              <div className="mt-2 flex flex-wrap gap-3">
                <button type="button" className="text-primary underline" onClick={() => setEditing(address.id)}>
                  {texts.editAddress}
                </button>
                {!address.isDefault && (
                  <button type="button" className="text-primary underline" disabled={busy} onClick={() => act(`/api/v1/account/addresses/${address.id}/default`, "POST")}>
                    {texts.setDefault}
                  </button>
                )}
                <button type="button" className="text-red-600 underline dark:text-red-400" disabled={busy} onClick={() => remove(address)}>
                  {texts.deleteAddress}
                </button>
              </div>
            </li>
          ),
        )}
      </ul>

      {editing === "new" ? (
        <AddressForm locale={locale} texts={texts} countries={countries} onDone={done} onCancel={addresses.length > 0 ? () => setEditing(null) : undefined} />
      ) : (
        editing === null && (
          <button type="button" onClick={() => setEditing("new")} className="h-11 self-start rounded-xl border border-border px-4 font-medium hover:bg-background">
            {texts.addAddress}
          </button>
        )
      )}
    </div>
  );
}

type FormProps = {
  locale: string;
  texts: Texts;
  countries: readonly string[];
  address?: Address;
  onDone: () => void;
  onCancel?: () => void;
};

/** Add (no address) or edit form. Field errors come back from the API with codes and are translated. */
export function AddressForm({ locale, texts, countries, address, onDone, onCancel }: FormProps) {
  const { busy, submit, formError, fieldError } = useApiForm(texts.errors);

  async function onSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const text = (name: string) => String(form.get(name) ?? "").trim();
    const body = {
      label: text("label") || null,
      recipientName: text("recipientName"),
      phone: text("phone"),
      countryCode: text("countryCode"),
      region: text("region") || null,
      city: text("city"),
      line1: text("line1"),
      line2: text("line2") || null,
      postalCode: text("postalCode") || null,
      isDefault: form.get("isDefault") === "on",
    };
    const result = address
      ? await submit(`/api/v1/account/addresses/${address.id}`, body, "PUT")
      : await submit("/api/v1/account/addresses", body);
    if (result.ok) onDone();
  }

  return (
    <form onSubmit={onSubmit} noValidate className="flex flex-col gap-3 rounded-xl border border-border p-3">
      <h2 className="font-semibold">{address ? texts.editAddress : texts.addAddress}</h2>
      {formError && <FormAlert kind="error">{formError}</FormAlert>}
      <FormField label={texts.recipientName} name="recipientName" autoComplete="shipping name" required maxLength={100} defaultValue={address?.recipientName} error={fieldError("recipientName")} />
      <FormField
        label={texts.recipientPhone}
        name="phone"
        type="tel"
        inputMode="tel"
        dir="ltr"
        autoComplete="shipping tel"
        required
        defaultValue={address?.phoneNumber}
        error={fieldError("phone")}
      />
      <SelectField
        label={texts.country}
        name="countryCode"
        autoComplete="shipping country"
        defaultValue={address?.countryCode ?? countries[0]}
        options={countries.map((c) => ({ value: c, label: regionName(c, locale) }))}
        error={fieldError("countryCode")}
      />
      <FormField label={texts.region} name="region" autoComplete="shipping address-level1" maxLength={100} defaultValue={address?.region ?? ""} error={fieldError("region")} />
      <FormField label={texts.city} name="city" autoComplete="shipping address-level2" required maxLength={100} defaultValue={address?.city} error={fieldError("city")} />
      <FormField label={texts.line1} name="line1" autoComplete="shipping address-line1" required maxLength={300} defaultValue={address?.line1} error={fieldError("line1")} />
      <FormField label={texts.line2} name="line2" autoComplete="shipping address-line2" maxLength={300} defaultValue={address?.line2 ?? ""} error={fieldError("line2")} />
      <FormField
        label={texts.postalCode}
        name="postalCode"
        autoComplete="shipping postal-code"
        inputMode="numeric"
        dir="ltr"
        maxLength={20}
        defaultValue={address?.postalCode ?? ""}
        error={fieldError("postalCode")}
      />
      <FormField label={texts.addressLabel} name="label" maxLength={50} defaultValue={address?.label ?? ""} error={fieldError("label")} />
      {!address?.isDefault && (
        <label className="flex items-center gap-2 text-sm">
          <input type="checkbox" name="isDefault" className="h-4 w-4 accent-[var(--primary)]" />
          {texts.makeDefault}
        </label>
      )}
      <div className="flex flex-wrap items-center gap-3">
        <SubmitButton busy={busy} label={texts.saveAddress} busyLabel={texts.working} />
        {onCancel && (
          <button type="button" onClick={onCancel} className="text-sm text-primary underline">
            {texts.cancel}
          </button>
        )}
      </div>
    </form>
  );
}
