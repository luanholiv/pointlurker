import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Switch from "./Switch";

describe("Switch", () => {
  it("has role=switch", () => {
    render(<Switch checked={false} onChange={vi.fn()} label="Notify" />);
    expect(screen.getByRole("switch", { name: "Notify" })).toBeInTheDocument();
  });

  it("aria-checked reflects checked=false", () => {
    render(<Switch checked={false} onChange={vi.fn()} label="Notify" />);
    expect(screen.getByRole("switch", { name: "Notify" })).toHaveAttribute(
      "aria-checked",
      "false",
    );
  });

  it("aria-checked reflects checked=true", () => {
    render(<Switch checked={true} onChange={vi.fn()} label="Notify" />);
    expect(screen.getByRole("switch", { name: "Notify" })).toHaveAttribute(
      "aria-checked",
      "true",
    );
  });

  it("calls onChange when clicked", async () => {
    const onChange = vi.fn();
    render(<Switch checked={false} onChange={onChange} label="Toggle" />);
    await userEvent.click(screen.getByRole("switch", { name: "Toggle" }));
    expect(onChange).toHaveBeenCalledOnce();
  });
});
