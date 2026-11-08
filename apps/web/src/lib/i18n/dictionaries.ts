import "server-only";
import type { Locale } from "./config";
import type fa from "./dictionaries/fa.json";

/** Every dictionary has the same shape as the Persian one (enforced by dictionaries.test.ts). */
export type Dictionary = typeof fa;

const loaders: Record<Locale, () => Promise<Dictionary>> = {
  fa: () => import("./dictionaries/fa.json").then((m) => m.default),
  en: () => import("./dictionaries/en.json").then((m) => m.default),
  hy: () => import("./dictionaries/hy.json").then((m) => m.default),
  de: () => import("./dictionaries/de.json").then((m) => m.default),
  ar: () => import("./dictionaries/ar.json").then((m) => m.default),
};

export function getDictionary(locale: Locale): Promise<Dictionary> {
  return loaders[locale]();
}
