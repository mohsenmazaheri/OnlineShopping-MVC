// @vitest-environment jsdom
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "@/lib/i18n/dictionaries/en.json";

const { router } = vi.hoisted(() => ({ router: { replace: vi.fn(), refresh: vi.fn() } }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));
vi.mock("next/link", () => ({ default: ({ href, children }: { href: string; children: React.ReactNode }) => <a href={href}>{children}</a> }));

import { LoginTabs } from "./LoginTabs";
import { PhoneCodeForm } from "./PhoneCodeForm";

const texts = en.account;
const fetchMock = vi.fn();
const respond = (status: number, body?: unknown) =>
  fetchMock.mockResolvedValueOnce(new Response(body === undefined ? null : JSON.stringify(body), { status }));
const bodyOf = (call: number) => JSON.parse(fetchMock.mock.calls[call][1].body as string);

beforeEach(() => vi.stubGlobal("fetch", fetchMock));
afterEach(() => {
  cleanup();
  fetchMock.mockReset();
  router.replace.mockReset();
  router.refresh.mockReset();
  vi.unstubAllGlobals();
  vi.useRealTimers();
});

async function requestCode(phone = "۰۹۱۲۱۲۳۴۵۶۷") {
  respond(200, { expiresInSeconds: 180, resendAfterSeconds: 60 });
  fireEvent.change(screen.getByLabelText(texts.phone), { target: { value: phone } });
  fireEvent.click(screen.getByRole("button", { name: texts.sendCode }));
  await screen.findByLabelText(texts.code);
}

describe("PhoneCodeForm (sign in)", () => {
  it("sends the number with ASCII digits, then signs in with the code", async () => {
    render(<PhoneCodeForm locale="fa" texts={texts} mode="signIn" returnTo="/fa/account" />);
    await requestCode();

    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/account/phone/send-code");
    expect(bodyOf(0)).toEqual({ phone: "09121234567", locale: "fa" });
    expect(screen.getByText(texts.codeSentTo.replace("{phone}", "09121234567"))).toBeTruthy();

    respond(200, { user: { id: "u1" }, isNewAccount: false });
    fireEvent.change(screen.getByLabelText(texts.code), { target: { value: "۴۸۲۹۱۳" } });
    fireEvent.click(screen.getByRole("button", { name: texts.verifyCode }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/fa/account"));
    expect(fetchMock.mock.calls[1][0]).toBe("/api/v1/account/phone/sign-in");
    expect(bodyOf(1)).toMatchObject({ phone: "09121234567", code: "482913", rememberMe: true });
  });

  it("welcomes a brand-new account on the account page", async () => {
    render(<PhoneCodeForm locale="fa" texts={texts} mode="signIn" returnTo="/fa" />);
    await requestCode();
    respond(200, { user: { id: "u1" }, isNewAccount: true });
    fireEvent.change(screen.getByLabelText(texts.code), { target: { value: "482913" } });
    fireEvent.click(screen.getByRole("button", { name: texts.verifyCode }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/fa/account?welcome=1"));
  });

  it("keeps only digits in the code box, at most six", async () => {
    render(<PhoneCodeForm locale="fa" texts={texts} mode="signIn" />);
    await requestCode();

    const input = screen.getByLabelText(texts.code) as HTMLInputElement;
    fireEvent.change(input, { target: { value: "12a-3 4567" } });

    expect(input.value).toBe("123456");
    expect(input.getAttribute("autocomplete")).toBe("one-time-code");
  });

  it("shows translated errors such as a wrong code", async () => {
    render(<PhoneCodeForm locale="fa" texts={texts} mode="signIn" />);
    await requestCode();
    respond(400, { code: "account.code_invalid" });
    fireEvent.change(screen.getByLabelText(texts.code), { target: { value: "000000" } });
    fireEvent.click(screen.getByRole("button", { name: texts.verifyCode }));

    expect((await screen.findByRole("alert")).textContent).toBe(texts.errors.code_invalid);
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("offers to resend only after the cooldown", async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    render(<PhoneCodeForm locale="fa" texts={texts} mode="signIn" />);
    await requestCode();

    expect(screen.queryByRole("button", { name: texts.resend })).toBeNull();
    expect(screen.getByText(texts.resendIn.replace("{seconds}", "60"))).toBeTruthy();

    // Async: lets React run the effect that starts the countdown interval before the fake time moves on
    // (the synchronous version raced with React's effect scheduling and failed on a busy machine).
    await act(async () => {
      await vi.advanceTimersByTimeAsync(61_000);
    });
    expect(screen.getByRole("button", { name: texts.resend })).toBeTruthy();
  });

  it("lets the user go back and fix the number", async () => {
    render(<PhoneCodeForm locale="fa" texts={texts} mode="signIn" />);
    await requestCode();

    fireEvent.click(screen.getByRole("button", { name: texts.changeNumber }));

    expect(screen.getByLabelText(texts.phone)).toBeTruthy();
  });
});

describe("PhoneCodeForm (add phone)", () => {
  it("uses the add-phone endpoints and confirms on success", async () => {
    render(<PhoneCodeForm locale="en" texts={texts} mode="addPhone" />);
    await requestCode("+44 7700 900123");
    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/account/phone/add/send-code");

    respond(200, { id: "u1" });
    fireEvent.change(screen.getByLabelText(texts.code), { target: { value: "123456" } });
    fireEvent.click(screen.getByRole("button", { name: texts.addPhoneSave }));

    expect(await screen.findByText(texts.phoneSaved)).toBeTruthy();
    expect(fetchMock.mock.calls[1][0]).toBe("/api/v1/account/phone/add/confirm");
    expect(bodyOf(1)).toEqual({ phone: "+44 7700 900123", code: "123456" });
    expect(router.refresh).toHaveBeenCalled();
  });
});

describe("LoginTabs", () => {
  it("shows only the password form when the shop has no SMS", () => {
    render(<LoginTabs locale="en" texts={texts} returnTo="/en" phoneCode={false} />);

    expect(screen.queryByRole("tablist")).toBeNull();
    expect(screen.getByLabelText(texts.identifier)).toBeTruthy();
  });

  it("starts with the SMS code and switches to the password", () => {
    render(<LoginTabs locale="fa" texts={texts} returnTo="/fa" phoneCode />);

    expect(screen.getByRole("tab", { name: texts.withCode }).getAttribute("aria-selected")).toBe("true");
    expect(screen.getByLabelText(texts.phone)).toBeTruthy();

    fireEvent.click(screen.getByRole("tab", { name: texts.withPassword }));
    expect(screen.getByLabelText(texts.identifier)).toBeTruthy();
  });
});
