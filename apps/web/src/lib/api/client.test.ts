import { afterEach, describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "./base-url";
import { ApiError, apiGet } from "./client";

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });

describe("API client", () => {
  afterEach(() => vi.unstubAllEnvs());

  it("uses the internal API URL without a trailing slash", () => {
    expect(apiBaseUrl({ API_INTERNAL_URL: "http://api:8080/" })).toBe("http://api:8080");
    expect(apiBaseUrl({})).toBe("http://localhost:5087");
  });

  it("returns the parsed JSON body", async () => {
    vi.stubEnv("API_INTERNAL_URL", "http://api:8080");
    const fetchMock = vi.fn().mockResolvedValue(json({ name: "Shop" }));

    await expect(apiGet<{ name: string }>("/api/v1/store", fetchMock)).resolves.toEqual({ name: "Shop" });
    expect(fetchMock).toHaveBeenCalledWith(
      "http://api:8080/api/v1/store",
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    );
  });

  it("turns problem details into an ApiError with status and code", async () => {
    const fetchMock = vi.fn().mockResolvedValue(json({ title: "No category.", code: "category.not_found" }, 404));

    const error = await apiGet("/api/v1/catalog/categories/x", fetchMock).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect(error).toMatchObject({ status: 404, code: "category.not_found" });
  });

  it("copes with error bodies that are not JSON", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response("oops", { status: 502, statusText: "Bad Gateway" }));

    await expect(apiGet("/api/v1/store", fetchMock)).rejects.toMatchObject({ status: 502, code: undefined });
  });

  it("rejects relative paths", async () => {
    await expect(apiGet("api/v1/store", vi.fn())).rejects.toThrow('must start with "/"');
  });
});
