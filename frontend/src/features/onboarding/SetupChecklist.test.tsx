import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import SetupChecklist from "./SetupChecklist";
import type { SetupStep } from "./useSetupProgress";

const allDone: SetupStep[] = [
  { id: "credential", done: true },
  { id: "store", done: true },
  { id: "wishlist", done: true },
];

const renderChecklist = (
  steps: SetupStep[],
  { autoRedeemDone = false, onFinish = () => {} } = {},
) =>
  render(
    <MemoryRouter>
      <SetupChecklist
        steps={steps}
        done={steps.filter((step) => step.done).length}
        total={steps.length}
        autoRedeemDone={autoRedeemDone}
        onFinish={onFinish}
      />
    </MemoryRouter>,
  );

describe("SetupChecklist", () => {
  it("offers only the first open step's action", () => {
    renderChecklist([
      { id: "credential", done: false },
      { id: "store", done: false },
      { id: "wishlist", done: false },
    ]);

    expect(
      screen.getByRole("link", { name: "Open credentials" }),
    ).toHaveAttribute("href", "/settings#token");
    expect(screen.queryByRole("link", { name: "Add a store" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Browse stores" })).toBeNull();
  });

  it("moves the action to the next step once one is done", () => {
    renderChecklist([
      { id: "credential", done: true },
      { id: "store", done: false },
      { id: "wishlist", done: false },
    ]);

    expect(screen.queryByRole("link", { name: "Open credentials" })).toBeNull();
    expect(screen.getByRole("link", { name: "Add a store" })).toHaveAttribute(
      "href",
      "/stores",
    );
  });

  it("names a done step as done for screen readers", () => {
    renderChecklist([
      { id: "credential", done: true },
      { id: "store", done: false },
      { id: "wishlist", done: false },
    ]);

    expect(
      screen.getByRole("heading", { name: /^Connect a provider\s*Done$/ }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "Add a store" }),
    ).toBeInTheDocument();
  });

  it("reports progress", () => {
    renderChecklist([
      { id: "credential", done: true },
      { id: "store", done: true },
      { id: "wishlist", done: false },
    ]);

    expect(screen.getByText("2 of 3 done")).toBeInTheDocument();
    expect(screen.getByRole("progressbar")).toHaveAttribute(
      "aria-valuenow",
      "2",
    );
  });

  it("lists auto-redeem as optional, with no action", () => {
    renderChecklist([
      { id: "credential", done: false },
      { id: "store", done: false },
      { id: "wishlist", done: false },
    ]);

    expect(
      screen.getByRole("heading", { name: /Turn on auto-redeem/ }),
    ).toHaveTextContent("Optional");
    expect(screen.queryByRole("link", { name: "Open wishlist" })).toBeNull();
  });

  it("offers no way out while a required step is open", () => {
    renderChecklist([
      { id: "credential", done: true },
      { id: "store", done: true },
      { id: "wishlist", done: false },
    ]);

    expect(screen.queryByRole("button", { name: "Finish setup" })).toBeNull();
    expect(screen.queryByText("You're all set")).toBeNull();
  });

  it("celebrates completion and finishes on request", async () => {
    const onFinish = vi.fn();
    renderChecklist(allDone, { onFinish });

    expect(
      screen.getByRole("heading", { name: "You're all set" }),
    ).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Finish setup" }));
    expect(onFinish).toHaveBeenCalledOnce();
  });

  it("offers auto-redeem once the required steps are done", () => {
    renderChecklist(allDone);

    expect(screen.getByRole("link", { name: "Open wishlist" })).toHaveAttribute(
      "href",
      "/wishlist",
    );
  });

  it("ticks auto-redeem once an entry has it on", () => {
    renderChecklist(allDone, { autoRedeemDone: true });

    expect(screen.queryByRole("link", { name: "Open wishlist" })).toBeNull();
    expect(
      screen.getByRole("heading", { name: /Turn on auto-redeem.*Done$/ }),
    ).toBeInTheDocument();
  });
});
