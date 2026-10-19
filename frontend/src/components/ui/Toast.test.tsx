import { fireEvent, render, screen } from "@testing-library/react";
import Toasts from "./Toast";

describe("Toasts", () => {
  it("renders an aria-live region for screen reader announcements", () => {
    render(
      <Toasts
        items={[{ id: "1", kicker: "Saved", msg: "Changes saved", open: true }]}
      />,
    );
    const liveRegion = document.querySelector("[aria-live]");
    expect(liveRegion).toBeInTheDocument();
  });

  it("renders toast title and message", () => {
    render(
      <Toasts
        items={[{ id: "1", kicker: "Sync", msg: "Item found", open: true }]}
      />,
    );
    expect(screen.getByText("Sync")).toBeInTheDocument();
    expect(screen.getByText("Item found")).toBeInTheDocument();
  });

  it("reports a toast the reader dismissed", () => {
    const onDismiss = vi.fn();
    render(
      <Toasts
        items={[{ id: "t1", kicker: "Sync", msg: "Item found", open: true }]}
        onDismiss={onDismiss}
      />,
    );
    const toast = screen.getByText("Item found").closest("li")!;
    toast.focus();
    fireEvent.keyDown(toast, { key: "Escape" });
    expect(onDismiss).toHaveBeenCalledWith("t1");
  });
});
