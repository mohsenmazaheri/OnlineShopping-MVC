"use client";

import { useState, type InputHTMLAttributes } from "react";
import { FormField } from "./FormParts";

type Props = Omit<InputHTMLAttributes<HTMLInputElement>, "type"> & {
  label: string;
  name: string;
  error?: string;
  hint?: string;
  /** Accessible names of the toggle button (translated). */
  showLabel: string;
  hideLabel: string;
};

/**
 * Password input with a show/hide button, so people can check what they typed (especially on phones).
 * The value is never changed or copied; only the input type switches between password and text.
 */
export function PasswordField({ showLabel, hideLabel, id, name, ...field }: Props) {
  const [visible, setVisible] = useState(false);
  const inputId = id ?? `field-${name}`;
  return (
    <FormField
      {...field}
      id={inputId}
      name={name}
      type={visible ? "text" : "password"}
      autoCapitalize="none"
      autoCorrect="off"
      spellCheck={false}
      adornment={
        <button
          type="button"
          onClick={() => setVisible((v) => !v)}
          aria-label={visible ? hideLabel : showLabel}
          aria-pressed={visible}
          aria-controls={inputId}
          title={visible ? hideLabel : showLabel}
          className="flex h-10 w-10 items-center justify-center rounded-lg text-muted hover:bg-background hover:text-foreground focus-visible:outline-2 focus-visible:outline-primary"
        >
          {visible ? <EyeOffIcon /> : <EyeIcon />}
        </button>
      }
    />
  );
}

// Simple outline icons drawn for this project (no icon font or external asset).
function EyeIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M2 12s3.6-7 10-7 10 7 10 7-3.6 7-10 7S2 12 2 12Z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );
}

function EyeOffIcon() {
  return (
    <svg aria-hidden="true" viewBox="0 0 24 24" width="20" height="20" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round">
      <path d="M10.6 5.1A10.4 10.4 0 0 1 12 5c6.4 0 10 7 10 7a17.6 17.6 0 0 1-2.7 3.6M6.6 6.6A17.4 17.4 0 0 0 2 12s3.6 7 10 7a9.9 9.9 0 0 0 5.4-1.6" />
      <path d="M9.9 9.9a3 3 0 0 0 4.2 4.2" />
      <path d="M3 3l18 18" />
    </svg>
  );
}
