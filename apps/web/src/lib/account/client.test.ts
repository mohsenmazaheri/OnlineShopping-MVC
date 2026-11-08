import { describe, expect, it, vi } from "vitest";
import { postJson } from "./client";

describe("postJson (browser API calls)", () => {
  it("posts JSON to the same origin with credentials and returns the data", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(JSON.stringify({ id: "u1" }), { status: 200 }));

    const result = await postJson("/api/v1/account/login", { identifier: "a@b.co" }, fetchMock);

    expect(result).toEqual({ ok: true, data: { id: "u1" } });
    expect(fetchMock).toHaveBeenCalledWith(
      "/api/v1/account/login",
      expect.objectContaining({ method: "POST", credentials: "same-origin", body: '{"identifier":"a@b.co"}' }),
    );
  });

  it("handles empty success bodies (204, 202)", async () => {
    const result = await postJson("/api/v1/account/logout", undefined, vi.fn().mockResolvedValue(new Response(null, { status: 204 })));

    expect(result).toEqual({ ok: true, data: null });
  });

  it("returns problem details for error responses", async () => {
    const body = JSON.stringify({ code: "account.email_taken" });
    const result = await postJson("/api/v1/account/register", {}, vi.fn().mockResolvedValue(new Response(body, { status: 409 })));

    expect(result).toEqual({ ok: false, problem: { status: 409, code: "account.email_taken", fieldErrors: {} } });
  });

  it("reports network failures as status 0 with the network code", async () => {
    const result = await postJson("/api/v1/account/login", {}, vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));

    expect(result).toEqual({ ok: false, problem: { status: 0, code: "network", fieldErrors: {} } });
  });

  it("survives a non-JSON error page from a proxy", async () => {
    const result = await postJson("/api/v1/account/login", {}, vi.fn().mockResolvedValue(new Response("<html>502</html>", { status: 502 })));

    expect(result).toEqual({ ok: false, problem: { status: 502, fieldErrors: {} } });
  });
});
