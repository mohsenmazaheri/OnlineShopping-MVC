// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "@/lib/i18n/dictionaries/en.json";

const { router } = vi.hoisted(() => ({ router: { replace: vi.fn(), refresh: vi.fn() } }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));
vi.mock("next/link", () => ({ default: ({ href, children }: { href: string; children: React.ReactNode }) => <a href={href}>{children}</a> }));
vi.mock("qrcode", () => ({ default: { toDataURL: vi.fn(async (uri: string) => `data:image/png;base64,${btoa(uri)}`) } }));

import { LoginTabs } from "./LoginTabs";
import { TwoFactorSettings } from "./TwoFactorSettings";
import { TwoFactorStep } from "./TwoFactorStep";

const texts = en.account;
const fetchMock = vi.fn();
const respond = (status: number, body?: unknown) =>
  fetchMock.mockResolvedValueOnce(new Response(body === undefined ? null : JSON.stringify(body), { status }));
const problem = (status: number, code: string) => respond(status, { status, code });
const bodyOf = (call: number) => JSON.parse(fetchMock.mock.calls[call][1].body as string);
const urlOf = (call: number) => fetchMock.mock.calls[call][0] as string;

const off = { enabled: false, recoveryCodesLeft: 0, rememberedDevice: false, requiredForAccount: false };
const on = { enabled: true, recoveryCodesLeft: 8, rememberedDevice: true, requiredForAccount: false };

beforeEach(() => vi.stubGlobal("fetch", fetchMock));
afterEach(() => {
  cleanup();
  fetchMock.mockReset();
  router.replace.mockReset();
  router.refresh.mockReset();
  vi.unstubAllGlobals();
});

describe("TwoFactorStep", () => {
  it("verifies the authenticator code (ASCII digits) and goes back", async () => {
    render(<TwoFactorStep texts={texts} returnTo="/en/account" />);
    respond(200, { id: "u1" });
    fireEvent.change(screen.getByLabelText(texts.authenticatorCode), { target: { value: "۱۲۳ ۴۵۶" } });
    fireEvent.click(screen.getByLabelText(texts.rememberDevice));
    fireEvent.click(screen.getByRole("button", { name: texts.verify }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/en/account"));
    expect(urlOf(0)).toBe("/api/v1/account/2fa/verify");
    expect(bodyOf(0)).toEqual({ code: "123456", rememberMe: true, rememberDevice: true });
  });

  it("signs in with a recovery code instead", async () => {
    render(<TwoFactorStep texts={texts} returnTo="/en" />);
    fireEvent.click(screen.getByRole("button", { name: texts.useRecoveryCode }));
    respond(200, { id: "u1" });
    fireEvent.change(screen.getByLabelText(texts.recoveryCode), { target: { value: "  abcde-12345 " } });
    fireEvent.click(screen.getByRole("button", { name: texts.verify }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/en"));
    expect(urlOf(0)).toBe("/api/v1/account/2fa/recovery");
    expect(bodyOf(0)).toEqual({ recoveryCode: "abcde-12345" });
  });

  it("shows the translated error and stays on the step", async () => {
    render(<TwoFactorStep texts={texts} returnTo="/en" />);
    problem(400, "account.code_invalid");
    fireEvent.change(screen.getByLabelText(texts.authenticatorCode), { target: { value: "000000" } });
    fireEvent.click(screen.getByRole("button", { name: texts.verify }));

    expect(await screen.findByText(texts.errors.code_invalid)).toBeTruthy();
    expect(router.replace).not.toHaveBeenCalled();
  });
});

describe("LoginTabs second step", () => {
  it("moves to the authenticator step when the password needs a second step", async () => {
    render(<LoginTabs locale="en" texts={texts} returnTo="/en" phoneCode={false} />);
    problem(403, "account.two_factor_required");
    fireEvent.change(screen.getByLabelText(texts.identifier), { target: { value: "a@b.test" } });
    fireEvent.change(screen.getByLabelText(texts.password), { target: { value: "secret-Pass1" } });
    fireEvent.click(screen.getByRole("button", { name: texts.submitLogin }));

    expect(await screen.findByLabelText(texts.authenticatorCode)).toBeTruthy();
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("starts at the step after a provider sign-in that needs it", () => {
    render(<LoginTabs locale="en" texts={texts} returnTo="/en" phoneCode twoFactor />);
    expect(screen.getByText(texts.twoFactorTitle)).toBeTruthy();
    expect(screen.queryByRole("tablist")).toBeNull();
  });
});

describe("TwoFactorSettings", () => {
  it("sets up with a locally generated QR code and shows the recovery codes once", async () => {
    render(<TwoFactorSettings texts={texts} status={off} />);
    respond(200, { sharedKey: "abcd efgh", authenticatorUri: "otpauth://totp/Shop:a@b.test?secret=ABCDEFGH" });
    fireEvent.click(screen.getByRole("button", { name: texts.enableTwoFactor }));

    const qr = (await screen.findByAltText("QR")) as HTMLImageElement;
    expect(qr.src).toBe(`data:image/png;base64,${btoa("otpauth://totp/Shop:a@b.test?secret=ABCDEFGH")}`);
    expect(screen.getByText("abcd efgh")).toBeTruthy();

    respond(200, { codes: ["code-1", "code-2"] });
    fireEvent.change(screen.getByLabelText(texts.authenticatorCode), { target: { value: "654321" } });
    fireEvent.click(screen.getByRole("button", { name: texts.enable }));

    expect(await screen.findByText("code-1")).toBeTruthy();
    expect(urlOf(1)).toBe("/api/v1/account/2fa/enable");
    expect(bodyOf(1)).toEqual({ code: "654321" });

    fireEvent.click(screen.getByRole("button", { name: texts.done }));
    expect(router.refresh).toHaveBeenCalled();
  });

  it("turning it off needs a current code", async () => {
    render(<TwoFactorSettings texts={texts} status={on} />);
    expect(screen.getByText(texts.recoveryCodesLeft.replace("{count}", "8"))).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: texts.disableTwoFactor }));

    respond(204);
    fireEvent.change(screen.getByLabelText(texts.authenticatorCode), { target: { value: "111222" } });
    fireEvent.click(screen.getByRole("button", { name: texts.disableTwoFactor }));

    await waitFor(() => expect(router.refresh).toHaveBeenCalled());
    expect(urlOf(0)).toBe("/api/v1/account/2fa/disable");
    expect(bodyOf(0)).toEqual({ code: "111222" });
  });

  it("renews recovery codes with a code and can forget this device", async () => {
    render(<TwoFactorSettings texts={texts} status={on} />);
    respond(204);
    fireEvent.click(screen.getByRole("button", { name: texts.forgetDevice }));
    await waitFor(() => expect(router.refresh).toHaveBeenCalled());
    expect(urlOf(0)).toBe("/api/v1/account/2fa/forget-device");

    fireEvent.click(screen.getByRole("button", { name: texts.newRecoveryCodes }));
    respond(200, { codes: ["fresh-1"] });
    fireEvent.change(screen.getByLabelText(texts.authenticatorCode), { target: { value: "333444" } });
    fireEvent.click(screen.getByRole("button", { name: texts.newRecoveryCodes }));

    expect(await screen.findByText("fresh-1")).toBeTruthy();
    expect(urlOf(1)).toBe("/api/v1/account/2fa/recovery-codes");
  });

  it("hides 'turn off' when the shop requires it for this account", () => {
    render(<TwoFactorSettings texts={texts} status={{ ...on, requiredForAccount: true }} />);
    expect(screen.queryByRole("button", { name: texts.disableTwoFactor })).toBeNull();
  });

  it("asks staff to turn it on when the shop requires it", () => {
    render(<TwoFactorSettings texts={texts} status={{ ...off, requiredForAccount: true }} />);
    expect(screen.getByText(texts.twoFactorRequired)).toBeTruthy();
  });
});
