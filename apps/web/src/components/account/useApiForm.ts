"use client";

import { useState } from "react";
import { postJson, type ApiResult } from "@/lib/account/client";
import { messageFor, problemMessage, type ErrorTexts, type Problem } from "@/lib/account/problems";

/** Error code meaning: the first factor was fine, now the authenticator step is needed. */
export const TWO_FACTOR_REQUIRED = "account.two_factor_required";

/** Shared submit/busy/error state for the account forms. */
export function useApiForm(errorTexts: ErrorTexts) {
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<Problem | null>(null);

  async function submit<T>(path: `/api/v1/${string}`, body: unknown): Promise<ApiResult<T>> {
    setBusy(true);
    setProblem(null);
    try {
      const result = await postJson<T>(path, body);
      if (!result.ok) setProblem(result.problem);
      return result;
    } finally {
      setBusy(false);
    }
  }

  return {
    busy,
    submit,
    /** Message for the whole form (business errors such as wrong password), or null. */
    formError: problem && (problem.code || Object.keys(problem.fieldErrors).length === 0) ? problemMessage(problem, errorTexts) : null,
    /** Message for one field's validation error, or undefined. */
    fieldError: (field: string) => {
      const code = problem?.fieldErrors[field]?.[0];
      return code ? messageFor(code, errorTexts) : undefined;
    },
  };
}
