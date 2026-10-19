import { render, screen } from "@testing-library/react";
import Pill from "./Pill";

describe("Pill", () => {
  it("renders children", () => {
    render(<Pill>Active</Pill>);
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("renders dot element", () => {
    const { container } = render(<Pill>x</Pill>);
    expect(container.querySelector(".dot")).toBeInTheDocument();
  });

  it("applies ok tone class", () => {
    const { container } = render(<Pill tone="ok">Online</Pill>);
    expect(container.querySelector(".pill.ok")).toBeInTheDocument();
  });

  it("applies warn tone class", () => {
    const { container } = render(<Pill tone="warn">Paused</Pill>);
    expect(container.querySelector(".pill.warn")).toBeInTheDocument();
  });

  it("applies bad tone class", () => {
    const { container } = render(<Pill tone="bad">Error</Pill>);
    expect(container.querySelector(".pill.bad")).toBeInTheDocument();
  });

  it("applies accent tone class", () => {
    const { container } = render(<Pill tone="accent">Streaming</Pill>);
    expect(container.querySelector(".pill.accent")).toBeInTheDocument();
  });

  it("default tone has no extra class", () => {
    const { container } = render(<Pill tone="default">N/A</Pill>);
    expect(container.querySelector(".pill")).toBeInTheDocument();
    expect(container.querySelector(".pill.ok")).not.toBeInTheDocument();
  });
});
