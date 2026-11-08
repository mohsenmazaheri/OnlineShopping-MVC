import { ImageResponse } from "next/og";
import { iconSizes } from "@/lib/pwa/manifest";

export function generateImageMetadata() {
  return iconSizes.map((size) => ({
    id: String(size),
    size: { width: size, height: size },
    contentType: "image/png",
  }));
}

/**
 * Generic placeholder app icon (a simple shopping-bag shape in our own colors). Phase 3 replaces it with the
 * store's uploaded logo. Drawn inside the maskable safe zone (central 80%).
 */
export default async function Icon({ id }: { id: Promise<string | number> }) {
  const size = Number(await id);
  const bag = Math.round(size * 0.5);
  return new ImageResponse(
    (
      <div style={{ width: "100%", height: "100%", display: "flex", alignItems: "center", justifyContent: "center", background: "#0f766e" }}>
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center" }}>
          <div
            style={{
              width: bag * 0.5,
              height: bag * 0.3,
              border: `${Math.max(2, Math.round(size * 0.03))}px solid #ffffff`,
              borderBottom: "none",
              borderRadius: `${bag}px ${bag}px 0 0`,
            }}
          />
          <div style={{ width: bag, height: bag * 0.8, background: "#ffffff", borderRadius: Math.round(size * 0.06) }} />
        </div>
      </div>
    ),
    { width: size, height: size },
  );
}
