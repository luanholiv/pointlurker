import { render, renderHook, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi } from "vitest";
import ToastProvider from "./ToastProvider";
import { useToast } from "./useToast";

const Raiser = ({
  label,
  msg,
  tone,
}: {
  label: string;
  msg: string;
  tone?: "error";
}) => {
  const { showToast } = useToast();
  return (
    <button onClick={() => showToast({ kicker: "Error", msg, tone })}>
      {label}
    </button>
  );
};

/**
 * Radix mounts the viewport as an `<ol class="toasts">` inside a labelled region, and appends a
 * separate `aria-live` announcer per toast. The viewport is therefore the stable "is there exactly
 * one stack" signal; counting live regions would just count toasts.
 */
const viewports = () => document.querySelectorAll("ol.toasts");

describe("ToastProvider", () => {
  it("mounts one toast viewport even with nothing to show", () => {
    render(
      <ToastProvider>
        <p>content</p>
      </ToastProvider>,
    );

    expect(viewports()).toHaveLength(1);
  });

  it("renders a toast raised by a descendant, carrying its tone", async () => {
    render(
      <ToastProvider>
        <Raiser label="fail" msg="Failed to load wishlist entry" tone="error" />
      </ToastProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "fail" }));

    const toast = await screen.findByText("Failed to load wishlist entry");
    expect(toast.closest("[data-tone]")).toHaveAttribute("data-tone", "error");
  });

  // The whole point of hoisting the toast state out of the pages: two components that
  // never see each other still write into one viewport.
  it("collects toasts from unrelated descendants into one viewport", async () => {
    render(
      <ToastProvider>
        <Raiser label="first" msg="First failure" />
        <div>
          <section>
            <Raiser label="second" msg="Second failure" />
          </section>
        </div>
      </ToastProvider>,
    );

    await userEvent.click(screen.getByRole("button", { name: "first" }));
    await userEvent.click(screen.getByRole("button", { name: "second" }));

    expect(viewports()).toHaveLength(1);
    const viewport = within(viewports()[0] as HTMLElement);
    expect(await viewport.findByText("First failure")).toBeInTheDocument();
    expect(await viewport.findByText("Second failure")).toBeInTheDocument();
  });

  // A no-op fallback outside the provider would swallow the message and recreate exactly
  // the silent failure this provider exists to fix, so the miswiring has to be loud.
  it("throws when useToast is called with no provider above it", () => {
    const errorSpy = vi.spyOn(console, "error").mockImplementation(() => {});

    expect(() => renderHook(() => useToast())).toThrow(/ToastProvider/);

    errorSpy.mockRestore();
  });
});
