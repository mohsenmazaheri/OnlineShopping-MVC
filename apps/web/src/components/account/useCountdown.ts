"use client";

import { useEffect, useState } from "react";

/** Seconds left until a deadline (ms timestamp), ticking once a second; 0 when passed or not set. */
export function useCountdown(deadline: number | null): number {
  const [now, setNow] = useState(() => Date.now());

  useEffect(() => {
    if (deadline === null) return;
    const timer = setInterval(() => setNow(Date.now()), 1000);
    return () => clearInterval(timer);
  }, [deadline]);

  return deadline === null ? 0 : Math.max(0, Math.ceil((deadline - now) / 1000));
}
