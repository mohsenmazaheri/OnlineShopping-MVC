/**
 * Language and country names from the browser/Node's built-in ICU data (Intl.DisplayNames): no lists to maintain
 * and nothing loaded from outside. Falls back to the code when the runtime has no name for it.
 */

/** A language's name in that language ("فارسی", "Deutsch"), as language pickers usually show it. */
export function nativeLanguageName(locale: string): string {
  return displayName(locale, "language", locale);
}

/** A country's name in the reader's language: regionName("DE", "fa") → "آلمان". */
export function regionName(countryCode: string, inLocale: string): string {
  return displayName(inLocale, "region", countryCode);
}

function displayName(inLocale: string, type: "language" | "region", code: string): string {
  try {
    return new Intl.DisplayNames([inLocale], { type, fallback: "code" }).of(code) ?? code;
  } catch {
    return code;
  }
}
