import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, useLocation } from "react-router";
import { useHashTab } from "./useHashTab";

const TABS = ["account", "token", "security"] as const;

const Harness = () => {
  const { activeTab, selectTab } = useHashTab(TABS, "account");
  const location = useLocation();
  return (
    <div>
      <div data-testid="active">{activeTab}</div>
      <div data-testid="pathname">{location.pathname}</div>
      <div data-testid="hash">{location.hash}</div>
      <div data-testid="search">{location.search}</div>
      {TABS.map((id) => (
        <button key={id} type="button" onClick={() => selectTab(id)}>
          {id}
        </button>
      ))}
    </div>
  );
};

const renderAt = (entry: string) =>
  render(
    <MemoryRouter initialEntries={[entry]}>
      <Harness />
    </MemoryRouter>,
  );

describe("useHashTab", () => {
  it("returns the default tab when no hash is present", () => {
    renderAt("/settings");
    expect(screen.getByTestId("active")).toHaveTextContent("account");
  });

  it("returns the tab matching the hash", () => {
    renderAt("/settings#security");
    expect(screen.getByTestId("active")).toHaveTextContent("security");
  });

  it("falls back to the default tab for an unknown hash", () => {
    renderAt("/settings#bogus");
    expect(screen.getByTestId("active")).toHaveTextContent("account");
  });

  it("selectTab sets the hash for non-default tabs", async () => {
    renderAt("/settings");
    await userEvent.click(screen.getByRole("button", { name: "token" }));
    expect(screen.getByTestId("hash")).toHaveTextContent("#token");
    expect(screen.getByTestId("active")).toHaveTextContent("token");
  });

  it("selectTab clears the hash for the default tab", async () => {
    renderAt("/settings#token");
    await userEvent.click(screen.getByRole("button", { name: "account" }));
    expect(screen.getByTestId("hash")).toBeEmptyDOMElement();
    expect(screen.getByTestId("active")).toHaveTextContent("account");
  });

  // selectTab navigates with `pathname` deliberately omitted, so it depends on the
  // router resolving the relative target against the current location. A regression
  // there would move the page off /settings entirely, unmounting it.
  it("preserves the pathname when switching tabs", async () => {
    renderAt("/settings");
    await userEvent.click(screen.getByRole("button", { name: "token" }));
    expect(screen.getByTestId("pathname")).toHaveTextContent("/settings");

    await userEvent.click(screen.getByRole("button", { name: "account" }));
    expect(screen.getByTestId("pathname")).toHaveTextContent("/settings");
  });

  it("preserves the query string when switching tabs", async () => {
    renderAt("/settings?q=sword&page=2");
    await userEvent.click(screen.getByRole("button", { name: "security" }));
    expect(screen.getByTestId("search")).toHaveTextContent("?q=sword&page=2");
    expect(screen.getByTestId("hash")).toHaveTextContent("#security");
    expect(screen.getByTestId("pathname")).toHaveTextContent("/settings");
  });
});
