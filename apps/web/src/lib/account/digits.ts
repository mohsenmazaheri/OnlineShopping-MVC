/** Persian (۰-۹) and Arabic-Indic (٠-٩) digits, as typed on Persian/Arabic keyboards, become 0-9. */
export function toAsciiDigits(value: string): string {
  return value.replace(/[۰-۹٠-٩]/g, (d) => {
    const code = d.charCodeAt(0);
    return String(code >= 0x06f0 ? code - 0x06f0 : code - 0x0660);
  });
}

/** Keeps only the digits of a one-time code (ASCII), capped at the code length. */
export function cleanCode(value: string, length = 6): string {
  return toAsciiDigits(value).replace(/\D/g, "").slice(0, length);
}

/** Fills {name} placeholders in a translated text. */
export function format(text: string, values: Record<string, string | number>): string {
  return text.replace(/\{(\w+)\}/g, (match, key: string) => (key in values ? String(values[key]) : match));
}
