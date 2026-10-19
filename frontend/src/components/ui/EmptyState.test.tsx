import { render, screen } from "@testing-library/react";
import { Store } from "lucide-react";
import EmptyState from "./EmptyState";

describe("EmptyState", () => {
  it("names what is missing as a heading", () => {
    render(<EmptyState icon={Store} title="No stores yet" />);
    expect(
      screen.getByRole("heading", { name: "No stores yet" }),
    ).toBeInTheDocument();
  });

  it("renders the body and the action", () => {
    render(
      <EmptyState
        icon={Store}
        title="No stores yet"
        body="Add one to start watching it."
        action={<button>Add store</button>}
      />,
    );
    expect(
      screen.getByText("Add one to start watching it."),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Add store" }),
    ).toBeInTheDocument();
  });

  it("defaults to the first-run tone", () => {
    const { container } = render(<EmptyState icon={Store} title="x" />);
    expect(container.firstChild).toHaveAttribute("data-tone", "first-run");
  });

  it("carries the tone it is given", () => {
    const { container } = render(
      <EmptyState icon={Store} title="x" tone="filtered" />,
    );
    expect(container.firstChild).toHaveAttribute("data-tone", "filtered");
  });

  it("renders no body or action slot when none is given", () => {
    const { container } = render(<EmptyState icon={Store} title="x" />);
    expect(container.querySelector(".empty-body")).toBeNull();
    expect(container.querySelector(".empty-action")).toBeNull();
  });
});
