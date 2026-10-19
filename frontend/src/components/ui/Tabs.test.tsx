import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import Tabs from "./Tabs";

const tabs = [
  { id: "items", label: "Items" },
  { id: "profile", label: "Profile" },
];

describe("Tabs", () => {
  it("renders a tablist with one tab button per definition", () => {
    render(<Tabs tabs={tabs} activeId="items" onSelect={() => {}} />);
    expect(screen.getByRole("tablist")).toBeInTheDocument();
    expect(screen.getAllByRole("tab")).toHaveLength(2);
  });

  it("marks the active tab with aria-selected", () => {
    render(<Tabs tabs={tabs} activeId="profile" onSelect={() => {}} />);
    expect(screen.getByRole("tab", { name: "Items" })).toHaveAttribute(
      "aria-selected",
      "false",
    );
    expect(screen.getByRole("tab", { name: "Profile" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
  });

  it("fires onSelect with the tab id on activation", async () => {
    const user = userEvent.setup();
    const onSelect = vi.fn();
    render(<Tabs tabs={tabs} activeId="items" onSelect={onSelect} />);
    await user.click(screen.getByRole("tab", { name: "Profile" }));
    expect(onSelect).toHaveBeenCalledWith("profile");
  });

  it("renders the indicator slot inside the tab button", () => {
    const withIndicator = [
      { id: "items", label: "Items" },
      {
        id: "profile",
        label: "Profile",
        indicator: <span data-testid="indicator" />,
      },
    ];
    render(<Tabs tabs={withIndicator} activeId="items" onSelect={() => {}} />);
    const profileTab = screen.getByRole("tab", { name: "Profile" });
    expect(profileTab).toContainElement(screen.getByTestId("indicator"));
  });
});
