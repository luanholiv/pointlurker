import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import MobileNav from "./MobileNav";

const renderNav = () =>
  render(
    <MemoryRouter initialEntries={["/stores"]}>
      <MobileNav />
    </MemoryRouter>,
  );

describe("MobileNav", () => {
  it("is closed until the menu button is pressed", () => {
    renderNav();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("opens a dialog listing every section", async () => {
    renderNav();
    await userEvent.click(screen.getByRole("button", { name: "Open menu" }));

    expect(screen.getByRole("dialog", { name: "Menu" })).toBeInTheDocument();
    for (const name of ["Stores", "Wishlist", "Redemptions", "Settings"]) {
      expect(
        screen.getByRole("link", { name: new RegExp(name) }),
      ).toBeInTheDocument();
    }
  });

  it("marks the current section", async () => {
    renderNav();
    await userEvent.click(screen.getByRole("button", { name: "Open menu" }));
    expect(screen.getByRole("link", { name: /Stores/ })).toHaveAttribute(
      "aria-current",
      "page",
    );
  });

  it("closes once a section is chosen", async () => {
    renderNav();
    await userEvent.click(screen.getByRole("button", { name: "Open menu" }));
    await userEvent.click(screen.getByRole("link", { name: /Wishlist/ }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("closes from its close button", async () => {
    renderNav();
    await userEvent.click(screen.getByRole("button", { name: "Open menu" }));
    await userEvent.click(screen.getByRole("button", { name: "Close menu" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
