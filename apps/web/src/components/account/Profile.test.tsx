// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "@/lib/i18n/dictionaries/en.json";
import type { Address } from "@/lib/account/session";

const { router } = vi.hoisted(() => ({ router: { replace: vi.fn(), refresh: vi.fn() } }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));
vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import { AccountNav } from "./AccountNav";
import { AddressBook } from "./AddressBook";
import { DeleteAccountForm } from "./DeleteAccountForm";
import { ChangePasswordForm, ProfileForm } from "./ProfileForms";

const texts = en.account;
const fetchMock = vi.fn();
const respond = (status: number, body?: unknown) =>
  fetchMock.mockResolvedValueOnce(new Response(body === undefined ? null : JSON.stringify(body), { status }));
const problem = (status: number, code: string, errors?: Record<string, string[]>) => respond(status, { status, code, errors });
const call = (n: number) => ({ url: fetchMock.mock.calls[n][0] as string, init: fetchMock.mock.calls[n][1] as RequestInit });
const bodyOf = (n: number) => JSON.parse(call(n).init.body as string);

const home: Address = {
  id: "a1", label: "Home", recipientName: "Ali Rezaei", phoneNumber: "+989121234567", countryCode: "IR", region: "Tehran",
  city: "Tehran", line1: "Valiasr St 10", line2: null, postalCode: "1234567890", isDefault: true,
};
const office: Address = { ...home, id: "a2", label: "Office", city: "Shiraz", isDefault: false };

beforeEach(() => vi.stubGlobal("fetch", fetchMock));
afterEach(() => {
  cleanup();
  fetchMock.mockReset();
  router.replace.mockReset();
  router.refresh.mockReset();
  vi.unstubAllGlobals();
});

describe("AccountNav", () => {
  it("links the three account pages and marks the current one", () => {
    render(<AccountNav locale="en" texts={texts} current="addresses" />);

    expect(screen.getByRole("link", { name: texts.navProfile }).getAttribute("href")).toBe("/en/account");
    expect(screen.getByRole("link", { name: texts.navAddresses }).getAttribute("aria-current")).toBe("page");
    expect(screen.getByRole("link", { name: texts.navPrivacy }).getAttribute("href")).toBe("/en/account/privacy");
  });
});

describe("ProfileForm", () => {
  it("offers the shop's languages by their own names and saves with PUT", async () => {
    render(<ProfileForm texts={texts} displayName="Ali" preferredLocale="fa" locales={["fa", "de"]} />);
    const language = screen.getByLabelText(texts.preferredLanguage) as HTMLSelectElement;
    expect([...language.options].map((o) => o.text)).toEqual(["فارسی", "Deutsch"]);

    respond(200, { id: "u1" });
    fireEvent.change(screen.getByLabelText(texts.displayName), { target: { value: "Ali R." } });
    fireEvent.change(language, { target: { value: "de" } });
    fireEvent.click(screen.getByRole("button", { name: texts.save }));

    expect(await screen.findByText(texts.saved)).toBeTruthy();
    expect(call(0)).toMatchObject({ url: "/api/v1/account/profile", init: { method: "PUT" } });
    expect(bodyOf(0)).toEqual({ displayName: "Ali R.", preferredLocale: "de" });
    expect(router.refresh).toHaveBeenCalled();
  });
});

describe("ChangePasswordForm", () => {
  it("sends both passwords and confirms the change", async () => {
    render(<ChangePasswordForm texts={texts} />);
    respond(200, true);
    fireEvent.change(screen.getByLabelText(texts.currentPassword), { target: { value: "old password 1" } });
    fireEvent.change(screen.getByLabelText(texts.newPassword), { target: { value: "new password 22" } });
    fireEvent.click(screen.getByRole("button", { name: texts.changePassword }));

    expect(await screen.findByText(texts.passwordChanged)).toBeTruthy();
    expect(bodyOf(0)).toEqual({ currentPassword: "old password 1", newPassword: "new password 22" });
  });

  it("shows a wrong current password", async () => {
    render(<ChangePasswordForm texts={texts} />);
    problem(400, "account.password_incorrect");
    fireEvent.click(screen.getByRole("button", { name: texts.changePassword }));

    expect(await screen.findByText(texts.errors.password_incorrect)).toBeTruthy();
  });
});

describe("AddressBook", () => {
  it("lists addresses with the default first and country names in the reader's language", () => {
    render(<AddressBook locale="en" texts={texts} addresses={[home, office]} countries={["IR"]} />);

    expect(screen.getAllByRole("listitem")).toHaveLength(2);
    expect(screen.getByText(texts.defaultAddress)).toBeTruthy();
    expect(screen.getByText(/Tehran, Tehran, Iran/)).toBeTruthy();
    expect(screen.getAllByRole("button", { name: texts.setDefault })).toHaveLength(1);
  });

  it("opens the form right away when the book is empty and saves a new address", async () => {
    render(<AddressBook locale="en" texts={texts} addresses={[]} countries={["AM", "DE"]} />);
    const country = screen.getByLabelText(texts.country) as HTMLSelectElement;
    expect([...country.options].map((o) => o.text)).toEqual(["Armenia", "Germany"]);

    respond(200, { ...home, id: "new" });
    fireEvent.change(screen.getByLabelText(texts.recipientName), { target: { value: " Anna " } });
    fireEvent.change(screen.getByLabelText(texts.recipientPhone), { target: { value: "077 123456" } });
    fireEvent.change(screen.getByLabelText(texts.city), { target: { value: "Yerevan" } });
    fireEvent.change(screen.getByLabelText(texts.line1), { target: { value: "Abovyan 1" } });
    fireEvent.click(screen.getByRole("button", { name: texts.saveAddress }));

    await waitFor(() => expect(router.refresh).toHaveBeenCalled());
    expect(call(0)).toMatchObject({ url: "/api/v1/account/addresses", init: { method: "POST" } });
    expect(bodyOf(0)).toEqual({
      label: null, recipientName: "Anna", phone: "077 123456", countryCode: "AM", region: null, city: "Yerevan",
      line1: "Abovyan 1", line2: null, postalCode: null, isDefault: false,
    });
  });

  it("shows translated field errors from the API", async () => {
    render(<AddressBook locale="en" texts={texts} addresses={[]} countries={["IR"]} />);
    problem(400, "", { city: ["account.address_city_required"] });
    fireEvent.click(screen.getByRole("button", { name: texts.saveAddress }));

    expect(await screen.findByText(texts.errors.address_city_required)).toBeTruthy();
  });

  it("edits with PUT, sets the default and deletes after confirming", async () => {
    vi.stubGlobal("confirm", vi.fn(() => true));
    render(<AddressBook locale="en" texts={texts} addresses={[home, office]} countries={["IR"]} />);

    fireEvent.click(screen.getAllByRole("button", { name: texts.editAddress })[0]);
    respond(200, home);
    fireEvent.click(screen.getByRole("button", { name: texts.saveAddress }));
    await waitFor(() => expect(call(0)).toMatchObject({ url: "/api/v1/account/addresses/a1", init: { method: "PUT" } }));

    respond(200, true);
    fireEvent.click(screen.getByRole("button", { name: texts.setDefault }));
    await waitFor(() => expect(call(1)).toMatchObject({ url: "/api/v1/account/addresses/a2/default", init: { method: "POST" } }));

    respond(200, true);
    fireEvent.click(screen.getAllByRole("button", { name: texts.deleteAddress })[1]);
    await waitFor(() => expect(call(2)).toMatchObject({ url: "/api/v1/account/addresses/a2", init: { method: "DELETE" } }));
  });

  it("does nothing when the deletion is not confirmed", () => {
    vi.stubGlobal("confirm", vi.fn(() => false));
    render(<AddressBook locale="en" texts={texts} addresses={[home]} countries={["IR"]} />);

    fireEvent.click(screen.getByRole("button", { name: texts.deleteAddress }));

    expect(fetchMock).not.toHaveBeenCalled();
  });
});

describe("DeleteAccountForm", () => {
  it("stays disabled until the customer confirms, then deletes with the password and goes home", async () => {
    render(<DeleteAccountForm locale="en" texts={texts} hasPassword />);
    const button = screen.getByRole("button", { name: texts.deleteAccount }) as HTMLButtonElement;
    expect(button.disabled).toBe(true);

    fireEvent.click(screen.getByLabelText(texts.deleteAccountConfirm));
    fireEvent.change(screen.getByLabelText(texts.password), { target: { value: "secret password 1" } });
    respond(200, true);
    fireEvent.click(button);

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/en"));
    expect(bodyOf(0)).toEqual({ password: "secret password 1" });
  });

  it("offers a fresh sign-in when a password-less account signed in too long ago", async () => {
    render(<DeleteAccountForm locale="fa" texts={texts} hasPassword={false} />);
    expect(screen.queryByLabelText(texts.password)).toBeNull();
    fireEvent.click(screen.getByLabelText(texts.deleteAccountConfirm));
    problem(403, "account.reauthentication_required");
    fireEvent.click(screen.getByRole("button", { name: texts.deleteAccount }));

    expect(await screen.findByText(texts.errors.reauthentication_required)).toBeTruthy();
    expect(bodyOf(0)).toEqual({ password: null });

    respond(204);
    fireEvent.click(screen.getByRole("button", { name: texts.signInAgain }));
    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/fa/account/login?returnUrl=%2Ffa%2Faccount%2Fprivacy"));
    expect(call(1).url).toBe("/api/v1/account/logout");
  });
});
