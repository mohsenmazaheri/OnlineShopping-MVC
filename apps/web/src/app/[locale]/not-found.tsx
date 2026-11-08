import Link from "next/link";
import { Suspense } from "react";
import { getDictionary } from "@/lib/i18n/dictionaries";
import { loadStore } from "@/lib/store/load";

async function NotFoundContent() {
  const store = await loadStore();
  const dict = await getDictionary(store.defaultLocale);
  return (
    <>
      <h1 className="text-xl font-bold">{dict.notFound.title}</h1>
      <Link href={`/${store.defaultLocale}`} className="text-primary underline">
        {dict.notFound.home}
      </Link>
    </>
  );
}

export default function NotFound() {
  return (
    <main className="mx-auto flex max-w-md flex-1 flex-col items-center justify-center gap-3 px-4 text-center">
      <Suspense fallback={null}>
        <NotFoundContent />
      </Suspense>
    </main>
  );
}
