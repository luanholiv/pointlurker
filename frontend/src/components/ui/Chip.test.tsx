import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Chip from "./Chip";

describe("Chip", () => {
  it("renders children", () => {
    render(<Chip>All</Chip>);
    expect(screen.getByText("All")).toBeInTheDocument();
  });

  it("aria-pressed is false when not pressed", () => {
    render(<Chip pressed={false}>All</Chip>);
    expect(screen.getByRole("button", { name: "All" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
  });

  it("aria-pressed is true when pressed", () => {
    render(<Chip pressed={true}>All</Chip>);
    expect(screen.getByRole("button", { name: "All" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("calls onToggle when clicked", async () => {
    const onToggle = vi.fn();
    render(<Chip onToggle={onToggle}>Active</Chip>);
    await userEvent.click(screen.getByRole("button", { name: "Active" }));
    expect(onToggle).toHaveBeenCalledOnce();
  });
});
