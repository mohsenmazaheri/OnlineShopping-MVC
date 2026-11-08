import type { Dictionary } from "@/lib/i18n/dictionaries";
import { format } from "@/lib/account/digits";

/** Brand names stay in their original form in every language. */
export const providerNames: Record<string, string> = { google: "Google", facebook: "Facebook", microsoft: "Microsoft" };

type Props = { providers: string[]; locale: string; returnTo: string; texts: Dictionary["account"] };

/**
 * "Continue with Google" etc. Plain links (full-page navigation): the API redirects to the provider and back.
 * Text-only on purpose: no provider logos, so no brand-guideline questions.
 */
export function ExternalLoginButtons({ providers, locale, returnTo, texts }: Props) {
  if (providers.length === 0) return null;
  const query = `returnUrl=${encodeURIComponent(returnTo)}&locale=${encodeURIComponent(locale)}`;

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center gap-3 text-xs text-muted" aria-hidden="true">
        <span className="h-px flex-1 bg-border" />
        {texts.or}
        <span className="h-px flex-1 bg-border" />
      </div>
      {providers.map((provider) => (
        <a
          key={provider}
          href={`/api/v1/account/external/${provider}/start?${query}`}
          className="flex h-12 items-center justify-center rounded-xl border border-border bg-surface font-medium hover:border-primary"
        >
          {format(texts.continueWith, { provider: providerNames[provider] ?? provider })}
        </a>
      ))}
    </div>
  );
}
