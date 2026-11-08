import { describe, expect, it, vi } from "vitest";
import { createStoreSettingsCache } from "./proxy-cache";
import { fallbackStore } from "./settings";

const ok = (body: unknown) => new Response(JSON.stringify(body), { status: 200 });
const store = { name: "Shop", defaultLocale: "en", locales: ["en", "de"] };

function setup(fetchImpl: ReturnType<typeof vi.fn>) {
  let time = 0;
  const get = createStoreSettingsCache(() => "http://api:8080", fetchImpl as unknown as typeof fetch, () => time);
  return { get, advance: (ms: number) => (time += ms) };
}

describe("store settings cache for the proxy", () => {
  it("asks the API once and reuses the answer for a minute", async () => {
    const fetchMock = vi.fn().mockImplementation(async () => ok(store));
    const { get, advance } = setup(fetchMock);

    expect((await get()).defaultLocale).toBe("en");
    advance(59_000);
    await get();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(fetchMock).toHaveBeenCalledWith("http://api:8080/api/v1/store", expect.anything());

    advance(2_000);
    await get();
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });

  it("serves the fallback when the API is down and retries after a few seconds", async () => {
    const fetchMock = vi.fn().mockRejectedValueOnce(new Error("ECONNREFUSED")).mockImplementation(async () => ok(store));
    const { get, advance } = setup(fetchMock);

    expect(await get()).toEqual(fallbackStore);
    advance(6_000);
    expect((await get()).name).toBe("Shop");
  });

  it("treats an error status as unavailable", async () => {
    const { get } = setup(vi.fn().mockResolvedValue(new Response("", { status: 503 })));

    expect(await get()).toEqual(fallbackStore);
  });

  it("sends a single request while one is in flight", async () => {
    let resolve!: (r: Response) => void;
    const fetchMock = vi.fn().mockReturnValue(new Promise<Response>((r) => (resolve = r)));
    const { get } = setup(fetchMock);

    const both = Promise.all([get(), get()]);
    resolve(ok(store));
    const [a, b] = await both;

    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(a).toEqual(b);
  });
});
