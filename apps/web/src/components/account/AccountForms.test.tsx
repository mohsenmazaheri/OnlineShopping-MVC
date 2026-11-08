// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "@/lib/i18n/dictionaries/en.json";

const { router } = vi.hoisted(() => ({ router: { replace: vi.fn(), refresh: vi.fn() } }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));
vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: ReactNode }) => (
    <a href={href} {...rest}>
      {children}
    </a>
  ),
}));

import { LogoutButton } from "./AccountActions";
import { LoginForm } from "./LoginForm";
import { ConfirmEmail, ForgotPasswordForm, ResetPasswordForm } from "./RecoveryForms";
import { RegisterForm } from "./RegisterForm";

const texts = en.account;
const fetchMock = vi.fn();

const respond = (status: number, body?: unknown) =>
  fetchMock.mockResolvedValueOnce(new Response(body === undefined ? null : JSON.stringify(body), { status }));

const type = (label: string, value: string) => fireEvent.change(screen.getByLabelText(label), { target: { value } });
const sentBody = (call = 0) => JSON.parse(fetchMock.mock.calls[call][1].body as string);

beforeEach(() => vi.stubGlobal("fetch", fetchMock));
afterEach(() => {
  cleanup();
  fetchMock.mockReset();
  router.replace.mockReset();
  router.refresh.mockReset();
  vi.unstubAllGlobals();
});

describe("LoginForm", () => {
  it("signs in and goes to the return address", async () => {
    respond(200, { id: "u1" });
    render(<LoginForm locale="en" texts={texts} returnTo="/en/account" />);

    type(texts.identifier, "ali@example.com");
    type(texts.password, "correct horse 42");
    fireEvent.click(screen.getByLabelText(texts.rememberMe));
    fireEvent.click(screen.getByRole("button", { name: texts.submitLogin }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/en/account"));
    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/account/login");
    expect(sentBody()).toEqual({ identifier: "ali@example.com", password: "correct horse 42", rememberMe: true });
  });

  it("shows the translated error and stays on the page", async () => {
    respond(400, { code: "account.invalid_credentials" });
    render(<LoginForm locale="en" texts={texts} returnTo="/en" />);

    fireEvent.click(screen.getByRole("button", { name: texts.submitLogin }));

    expect(await screen.findByRole("alert")).toHaveProperty("textContent", texts.errors.invalid_credentials);
    expect(router.replace).not.toHaveBeenCalled();
  });

  it("explains rate limiting", async () => {
    respond(429);
    render(<LoginForm locale="en" texts={texts} returnTo="/en" />);

    fireEvent.click(screen.getByRole("button", { name: texts.submitLogin }));

    expect(await screen.findByRole("alert")).toHaveProperty("textContent", texts.errors.too_many_requests);
  });
});

describe("RegisterForm", () => {
  it("signs up by email only (mobile numbers sign up with an SMS code)", () => {
    render(<RegisterForm locale="en" texts={texts} />);

    expect(screen.getByLabelText(texts.email)).toBeTruthy();
    expect(screen.queryByLabelText(texts.phone)).toBeNull();
  });

  it("sends the locale and an empty name as null, then opens the account page", async () => {
    respond(200, { id: "u1" });
    render(<RegisterForm locale="en" texts={texts} />);

    type(texts.email, " ali@example.com ");
    type(texts.password, "correct horse 42");
    fireEvent.click(screen.getByRole("button", { name: texts.submitRegister }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/en/account"));
    expect(sentBody()).toEqual({ email: "ali@example.com", password: "correct horse 42", displayName: null, locale: "en" });
  });

  it("puts validation errors next to the field", async () => {
    respond(400, { errors: { password: ["account.password_too_short"] } });
    render(<RegisterForm locale="en" texts={texts} />);

    fireEvent.click(screen.getByRole("button", { name: texts.submitRegister }));

    expect(await screen.findByText(texts.errors.password_too_short)).toBeTruthy();
    expect(screen.getByLabelText(texts.password).getAttribute("aria-invalid")).toBe("true");
    expect(screen.queryByRole("alert")).toBeNull();
  });
});

describe("Password recovery", () => {
  it("always confirms that a reset link was sent", async () => {
    respond(202);
    render(<ForgotPasswordForm locale="fa" texts={texts} />);

    type(texts.email, "nobody@example.com");
    fireEvent.click(screen.getByRole("button", { name: texts.submitForgot }));

    expect(await screen.findByText(texts.forgotSent)).toBeTruthy();
    expect(sentBody()).toEqual({ email: "nobody@example.com", locale: "fa" });
  });

  it("resets the password with the token from the link", async () => {
    respond(200, true);
    render(<ResetPasswordForm locale="en" texts={texts} userId="u1" token="t+1" />);

    type(texts.newPassword, "brand new pass 7");
    fireEvent.click(screen.getByRole("button", { name: texts.submitReset }));

    expect(await screen.findByText(texts.resetDone)).toBeTruthy();
    expect(sentBody()).toEqual({ userId: "u1", token: "t+1", newPassword: "brand new pass 7" });
  });

  it("explains an incomplete link instead of showing the form", () => {
    render(<ResetPasswordForm locale="en" texts={texts} userId="" token="" />);

    expect(screen.getByRole("alert").textContent).toBe(texts.errors.token_required);
  });
});

describe("Email confirmation", () => {
  it("confirms once when the page opens", async () => {
    respond(200, true);
    render(<ConfirmEmail locale="en" texts={texts} userId="u1" token="tok" />);

    expect(await screen.findByText(texts.confirmOk)).toBeTruthy();
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("shows why a confirmation failed", async () => {
    respond(400, { code: "account.invalid_token" });
    render(<ConfirmEmail locale="en" texts={texts} userId="u1" token="old" />);

    expect(await screen.findByText(texts.errors.invalid_token)).toBeTruthy();
  });
});

describe("LogoutButton", () => {
  it("signs out and returns home", async () => {
    respond(204);
    render(<LogoutButton locale="fa" texts={texts} />);

    fireEvent.click(screen.getByRole("button", { name: texts.logout }));

    await waitFor(() => expect(router.replace).toHaveBeenCalledWith("/fa"));
    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/account/logout");
  });
});
