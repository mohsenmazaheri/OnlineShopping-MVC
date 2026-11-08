import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { revalidateTag } = vi.hoisted(() => ({ revalidateTag: vi.fn() }));
vi.mock("next/cache", () => ({ revalidateTag, cacheTag: vi.fn(), cacheLife: vi.fn() }));

import { POST } from "./route";

const request = (body: unknown, secret?: string) =>
  new Request("http://localhost/api/revalidate", {
    method: "POST",
    headers: { "Content-Type": "application/json", ...(secret ? { "x-revalidate-secret": secret } : {}) },
    body: typeof body === "string" ? body : JSON.stringify(body),
  });

describe("POST /api/revalidate", () => {
  beforeEach(() => vi.stubEnv("REVALIDATE_SECRET", "s3cret-value"));
  afterEach(() => {
    vi.unstubAllEnvs();
    revalidateTag.mockReset();
  });

  it("rejects a missing or wrong secret", async () => {
    expect((await POST(request({ tags: ["catalog"] }))).status).toBe(401);
    expect((await POST(request({ tags: ["catalog"] }, "wrong"))).status).toBe(401);
    expect(revalidateTag).not.toHaveBeenCalled();
  });

  it("is disabled when no secret is configured", async () => {
    vi.stubEnv("REVALIDATE_SECRET", "");
    expect((await POST(request({ tags: ["catalog"] }, "anything"))).status).toBe(401);
  });

  it("revalidates known tags with stale-while-revalidate", async () => {
    const response = await POST(request({ tags: ["catalog", "store"] }, "s3cret-value"));

    expect(response.status).toBe(200);
    expect(revalidateTag).toHaveBeenCalledWith("catalog", "max");
    expect(revalidateTag).toHaveBeenCalledWith("store", "max");
  });

  it("rejects unknown tags and invalid bodies", async () => {
    expect((await POST(request({ tags: ["everything"] }, "s3cret-value"))).status).toBe(400);
    expect((await POST(request({ tags: [] }, "s3cret-value"))).status).toBe(400);
    expect((await POST(request("not json", "s3cret-value"))).status).toBe(400);
    expect(revalidateTag).not.toHaveBeenCalled();
  });
});
