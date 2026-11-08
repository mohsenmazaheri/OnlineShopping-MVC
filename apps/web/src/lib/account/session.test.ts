import { afterEach, describe, expect, it, vi } from "vitest";

const { cookieStore } = vi.hoisted(() => ({ cookieStore: new Map<string, string>() }));
vi.mock("next/headers", () => ({
  cookies: async () => ({
    get: (name: string) => (cookieStore.has(name) ? { name, value: cookieStore.get(name) } : undefined),
  }),
}));

import { getCurrentUser, getExternalLogins } from "./session";

describe("getCurrentUser (server rendering)", () => {
  afterEach(() => cookieStore.clear());

  it("returns null without asking the API when there is no session cookie", async () => {
    const fetchMock = vi.fn();

    expect(await getCurrentUser(fetchMock)).toBeNull();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("forwards only the session cookie, uncached, and returns the user", async () => {
    cookieStore.set("os_session", "abc");
    const user = { id: "u1", roles: ["Customer"], permissions: [] };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(user), { status: 200 }));

    expect(await getCurrentUser(fetchMock)).toEqual(user);
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/v1\/account\/me$/),
      expect.objectContaining({ cache: "no-store", headers: expect.objectContaining({ Cookie: "os_session=abc" }) }),
    );
  });

  it("treats an expired session or an unreachable API as signed out", async () => {
    cookieStore.set("os_session", "expired");

    expect(await getCurrentUser(vi.fn().mockResolvedValue(new Response(null, { status: 401 })))).toBeNull();
    expect(await getCurrentUser(vi.fn().mockRejectedValue(new Error("ECONNREFUSED")))).toBeNull();
  });
});

describe("getExternalLogins", () => {
  afterEach(() => cookieStore.clear());

  it("asks the API with the session cookie and returns the providers", async () => {
    cookieStore.set("os_session", "abc");
    const view = { available: ["google"], linked: [] };
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify(view), { status: 200 }));

    expect(await getExternalLogins(fetchMock)).toEqual(view);
    expect(fetchMock).toHaveBeenCalledWith(expect.stringMatching(/\/api\/v1\/account\/external$/), expect.objectContaining({ cache: "no-store" }));
  });

  it("returns null without a session or when the API fails", async () => {
    expect(await getExternalLogins(vi.fn())).toBeNull();
    cookieStore.set("os_session", "abc");
    expect(await getExternalLogins(vi.fn().mockResolvedValue(new Response(null, { status: 401 })))).toBeNull();
  });
});
