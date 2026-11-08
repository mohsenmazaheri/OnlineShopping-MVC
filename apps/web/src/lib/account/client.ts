import { parseProblem, type Problem } from "./problems";

export type ApiResult<T> = { ok: true; data: T } | { ok: false; problem: Problem };

export type Method = "POST" | "PUT" | "DELETE";

/**
 * Send JSON to the API from the browser. Same origin (/api/v1 is forwarded by proxy.ts), so the HttpOnly session
 * cookie travels automatically and is never visible to JavaScript. Network failures come back as status 0.
 */
export async function sendJson<T>(
  method: Method,
  path: `/api/v1/${string}`,
  body?: unknown,
  fetchImpl: typeof fetch = fetch,
): Promise<ApiResult<T>> {
  let response: Response;
  try {
    response = await fetchImpl(path, {
      method,
      credentials: "same-origin",
      headers: { "Content-Type": "application/json", Accept: "application/json" },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    return { ok: false, problem: { status: 0, code: "network", fieldErrors: {} } };
  }

  const text = await response.text();
  const json: unknown = text ? safeJson(text) : null;
  if (!response.ok) return { ok: false, problem: parseProblem(response.status, json) };
  return { ok: true, data: json as T };
}

/** POST JSON to the API from the browser (see {@link sendJson}). */
export function postJson<T>(path: `/api/v1/${string}`, body?: unknown, fetchImpl: typeof fetch = fetch): Promise<ApiResult<T>> {
  return sendJson<T>("POST", path, body, fetchImpl);
}

function safeJson(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}
