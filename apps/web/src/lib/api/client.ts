import "server-only";
import { apiBaseUrl } from "./base-url";

export { apiBaseUrl };

/** Error from the backend API, carrying the HTTP status and the problem-details `code` when present. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly code: string | undefined,
    message: string,
  ) {
    super(message);
    this.name = "ApiError";
  }
}

const timeoutMs = 5000;

/**
 * GET a JSON resource from the API. Runs on the server only (React Server Components, route handlers),
 * so the API stays on the private network and credentials never reach the browser.
 */
export async function apiGet<T>(path: string, fetchImpl: typeof fetch = fetch): Promise<T> {
  if (!path.startsWith("/")) throw new Error(`API path must start with "/": ${path}`);

  const response = await fetchImpl(`${apiBaseUrl()}${path}`, {
    headers: { Accept: "application/json" },
    signal: AbortSignal.timeout(timeoutMs),
  });

  if (!response.ok) {
    let code: string | undefined;
    let title = response.statusText;
    try {
      const problem = (await response.json()) as { code?: string; title?: string };
      code = problem.code;
      title = problem.title ?? title;
    } catch {
      // Not a problem-details body; keep the status text.
    }
    throw new ApiError(response.status, code, `GET ${path} failed with ${response.status}: ${title}`);
  }

  return (await response.json()) as T;
}
