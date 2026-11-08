// @vitest-environment jsdom
import { cleanup, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { CategoryGrid, CategoryGridSkeleton } from "./CategoryGrid";

afterEach(cleanup);

describe("CategoryGrid", () => {
  it("shows each category with its sub-categories", () => {
    render(
      <CategoryGrid
        heading="Categories"
        emptyText="None"
        categories={[
          { id: "1", name: "Books", slug: "books", subCategories: [{ id: "2", name: "Poetry", slug: "poetry" }] },
          { id: "3", name: "Toys", slug: "toys", subCategories: [] },
        ]}
      />,
    );

    const section = screen.getByRole("region", { name: "Categories" });
    expect(within(section).getByRole("heading", { level: 3, name: "Books" })).toBeTruthy();
    expect(within(section).getByText("Poetry")).toBeTruthy();
    expect(within(section).getByRole("heading", { level: 3, name: "Toys" })).toBeTruthy();
    expect(screen.queryByText("None")).toBeNull();
  });

  it("shows the empty text when there are no categories", () => {
    render(<CategoryGrid heading="Categories" emptyText="No categories yet." categories={[]} />);

    expect(screen.getByText("No categories yet.")).toBeTruthy();
  });

  it("announces the loading skeleton to screen readers", () => {
    render(<CategoryGridSkeleton label="Loading…" />);

    expect(screen.getByRole("status", { name: "Loading…" })).toBeTruthy();
  });
});
