/** Error codes returned by the API (problem details "code", or validation "errors" values). */
export type ErrorCode = string;

export type Problem = {
  status: number;
  /** Business error code, e.g. "account.invalid_credentials". */
  code?: ErrorCode;
  /** Field name → validation error codes, e.g. { password: ["account.password_too_short"] }. */
  fieldErrors: Record<string, ErrorCode[]>;
};

/** Reads an RFC 9457 problem-details body as returned by the API (see ResultExtensions / ValidationFilter). */
export function parseProblem(status: number, body: unknown): Problem {
  const problem: Problem = { status, fieldErrors: {} };
  if (typeof body !== "object" || body === null) return problem;

  const record = body as Record<string, unknown>;
  if (typeof record.code === "string") problem.code = record.code;

  if (typeof record.errors === "object" && record.errors !== null) {
    for (const [field, codes] of Object.entries(record.errors as Record<string, unknown>)) {
      if (Array.isArray(codes)) problem.fieldErrors[field] = codes.filter((c): c is string => typeof c === "string");
    }
  }
  return problem;
}

/** Translations for error codes: the key is the code without its "area." prefix. */
export type ErrorTexts = Record<string, string> & { generic: string; network: string; too_many_requests: string };

/** A user-facing message for an error code; unknown codes get the generic text, never the raw code. */
export function messageFor(code: ErrorCode | undefined, texts: ErrorTexts): string {
  if (!code) return texts.generic;
  const key = code.includes(".") ? code.slice(code.indexOf(".") + 1) : code;
  return texts[key] ?? texts.generic;
}

/** The message for a whole problem: business code first, then the first field error, then by status. */
export function problemMessage(problem: Problem, texts: ErrorTexts): string {
  if (problem.status === 429) return texts.too_many_requests;
  if (problem.code) return messageFor(problem.code, texts);
  const firstFieldCode = Object.values(problem.fieldErrors).flat()[0];
  return messageFor(firstFieldCode, texts);
}
