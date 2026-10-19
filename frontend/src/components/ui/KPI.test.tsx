import { render, screen } from "@testing-library/react";
import KPI from "./KPI";

describe("KPI", () => {
  it("renders label and value", () => {
    render(<KPI label="Stores watched" value="09" />);
    expect(screen.getByText("Stores watched")).toBeInTheDocument();
    expect(screen.getByText("09")).toBeInTheDocument();
  });

  it("renders delta when provided", () => {
    render(<KPI label="Redemptions" value="11" delta="+3 vs prev" />);
    expect(screen.getByText("+3 vs prev")).toBeInTheDocument();
  });

  it("applies bad class on negative delta", () => {
    const { container } = render(
      <KPI label="Latency" value="1.4s" delta="+0.2s" deltaTone="bad" />,
    );
    expect(container.querySelector(".delta.bad")).toBeInTheDocument();
  });

  it("no bad class on ok delta", () => {
    const { container } = render(
      <KPI label="Items" value="03" delta="+2" deltaTone="ok" />,
    );
    expect(container.querySelector(".delta.bad")).not.toBeInTheDocument();
  });

  it("does not render delta when not provided", () => {
    const { container } = render(<KPI label="Stores" value="09" />);
    expect(container.querySelector(".delta")).not.toBeInTheDocument();
  });
});
