import { timingSafeEqual } from "node:crypto";
import { revalidateTag } from "next/cache";
import { cacheTags } from "@/lib/api/storefront";

const knownTags = new Set<string>(Object.values(cacheTags));

function secretMatches(provided: string | null, expected: string | undefined): boolean {
  if (!provided || !expected) return false;
  const a = Buffer.from(provided);
  const b = Buffer.from(expected);
  return a.length === b.length && timingSafeEqual(a, b);
}

/**
 * Called by the API when store or catalog data changes, so cached pages update immediately.
 * POST /api/revalidate with header "x-revalidate-secret" and body {"tags":["catalog"]}.
 */
export async function POST(request: Request): Promise<Response> {
  if (!secretMatches(request.headers.get("x-revalidate-secret"), process.env.REVALIDATE_SECRET)) {
    return Response.json({ error: "unauthorized" }, { status: 401 });
  }

  let tags: unknown;
  try {
    ({ tags } = (await request.json()) as { tags?: unknown });
  } catch {
    return Response.json({ error: "invalid_body" }, { status: 400 });
  }

  if (!Array.isArray(tags) || tags.length === 0 || !tags.every((t) => typeof t === "string" && knownTags.has(t))) {
    return Response.json({ error: "unknown_tags", known: [...knownTags] }, { status: 400 });
  }

  for (const tag of tags as string[]) revalidateTag(tag, "max");
  return Response.json({ revalidated: tags });
}
