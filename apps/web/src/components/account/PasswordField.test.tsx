// @vitest-environment jsdom
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { PasswordField } from "./PasswordField";

afterEach(cleanup);

const renderField = (error?: string) =>
  render(
    <PasswordField label="Password" name="password" autoComplete="new-password" hint="At least 8" error={error} showLabel="Show password" hideLabel="Hide password" />,
  );

describe("PasswordField", () => {
  it("starts hidden and shows the typed value on demand without changing it", () => {
    renderField();
    const input = screen.getByLabelText("Password") as HTMLInputElement;
    fireEvent.change(input, { target: { value: "secret pass 1" } });
    expect(input.type).toBe("password");

    fireEvent.click(screen.getByRole("button", { name: "Show password" }));

    expect(input.type).toBe("text");
    expect(input.value).toBe("secret pass 1");
    const toggle = screen.getByRole("button", { name: "Hide password" });
    expect(toggle.getAttribute("aria-pressed")).toBe("true");
    expect(toggle.getAttribute("aria-controls")).toBe(input.id);

    fireEvent.click(toggle);
    expect(input.type).toBe("password");
  });

  it("is a plain button that never submits the form", () => {
    renderField();

    expect(screen.getByRole("button", { name: "Show password" }).getAttribute("type")).toBe("button");
  });

  it("keeps the label, hint, error and browser hints of a normal field", () => {
    renderField("Too short");
    const input = screen.getByLabelText("Password");

    expect(input.getAttribute("autocomplete")).toBe("new-password");
    expect(input.getAttribute("autocapitalize")).toBe("none");
    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByText("Too short")).toBeTruthy();
  });
});
