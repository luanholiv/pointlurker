import { render, screen } from "@testing-library/react";
import NotificationDot from "./NotificationDot";

describe("NotificationDot", () => {
  it("renders a warn-tone dot element", () => {
    const { container } = render(<NotificationDot label="Needs attention" />);
    expect(container.querySelector(".notification-dot")).toBeInTheDocument();
  });

  it("renders the label as screen-reader-only text", () => {
    render(<NotificationDot label="Profile needs attention" />);
    const label = screen.getByText("Profile needs attention");
    expect(label).toBeInTheDocument();
    expect(label).toHaveClass("sr-only");
  });

  it("has no visible text content", () => {
    const { container } = render(<NotificationDot label="hidden" />);
    const root = container.firstElementChild!;
    const visibleText = Array.from(root.childNodes)
      .filter(
        (node) =>
          node.nodeType === Node.TEXT_NODE ||
          (node instanceof Element && !node.classList.contains("sr-only")),
      )
      .map((node) => node.textContent?.trim() ?? "")
      .join("");
    expect(visibleText).toBe("");
  });
});
