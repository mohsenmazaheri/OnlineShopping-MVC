// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import en from "@/lib/i18n/dictionaries/en.json";

const { router } = vi.hoisted(() => ({ router: { replace: vi.fn(), refresh: vi.fn() } }));
vi.mock("next/navigation", () => ({ useRouter: () => router }));

import { ExternalLoginButtons } from "./ExternalLoginButtons";
import { LinkedAccounts } from "./LinkedAccounts";

const texts = en.account;
const fetchMock = vi.fn();

beforeEach(() => vi.stubGlobal("fetch", fetchMock));
afterEach(() => {
  cleanup();
  fetchMock.mockReset();
  router.refresh.mockReset();
  vi.unstubAllGlobals();
});

describe("ExternalLoginButtons", () => {
  it("links each configured provider to the API start endpoint with the return address", () => {
    render(<ExternalLoginButtons providers={["google", "microsoft"]} locale="fa" returnTo="/fa/account?x=1" texts={texts} />);

    const google = screen.getByRole("link", { name: "Continue with Google" });
    expect(google.getAttribute("href")).toBe("/api/v1/account/external/google/start?returnUrl=%2Ffa%2Faccount%3Fx%3D1&locale=fa");
    expect(screen.getByRole("link", { name: "Continue with Microsoft" })).toBeTruthy();
    expect(screen.queryByRole("link", { name: /Facebook/ })).toBeNull();
  });

  it("renders nothing when the shop offers no providers", () => {
    const { container } = render(<ExternalLoginButtons providers={[]} locale="fa" returnTo="/fa" texts={texts} />);

    expect(container.innerHTML).toBe("");
  });
});

describe("LinkedAccounts", () => {
  it("offers to link unlinked providers and to unlink linked ones", () => {
    render(<LinkedAccounts locale="en" texts={texts} available={["google", "facebook"]} linked={["google"]} />);

    expect(screen.getByRole("button", { name: texts.unlink })).toBeTruthy();
    expect(screen.getByRole("link", { name: texts.link }).getAttribute("href")).toBe("/api/v1/account/external/facebook/link?locale=en");
  });

  it("unlinks and refreshes the page", async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ available: ["google"], linked: [] }), { status: 200 }));
    render(<LinkedAccounts locale="en" texts={texts} available={["google"]} linked={["google"]} />);

    fireEvent.click(screen.getByRole("button", { name: texts.unlink }));

    await waitFor(() => expect(router.refresh).toHaveBeenCalled());
    expect(fetchMock.mock.calls[0][0]).toBe("/api/v1/account/external/google/unlink");
  });

  it("explains why the last sign-in method cannot be unlinked", async () => {
    fetchMock.mockResolvedValueOnce(new Response(JSON.stringify({ code: "account.external_last_method" }), { status: 400 }));
    render(<LinkedAccounts locale="en" texts={texts} available={["google"]} linked={["google"]} />);

    fireEvent.click(screen.getByRole("button", { name: texts.unlink }));

    expect((await screen.findByRole("alert")).textContent).toBe(texts.errors.external_last_method);
    expect(router.refresh).not.toHaveBeenCalled();
  });

  it("renders nothing without providers", () => {
    const { container } = render(<LinkedAccounts locale="en" texts={texts} available={[]} linked={[]} />);

    expect(container.innerHTML).toBe("");
  });
});
