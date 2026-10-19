import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import Sidebar from "./Sidebar";

const renderSidebar = (path: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <Sidebar />
    </MemoryRouter>,
  );

describe("Sidebar", () => {
  it("stores nav item has aria-current=page at /stores", () => {
    renderSidebar("/stores");
    const storesLink = screen.getByRole("link", { name: /Stores/i });
    expect(storesLink).toHaveAttribute("aria-current", "page");
  });

  it("stores nav item has aria-current=page on store detail route", () => {
    renderSidebar("/stores/ale_apoka");
    const storesLink = screen.getByRole("link", { name: /Stores/i });
    expect(storesLink).toHaveAttribute("aria-current", "page");
  });

  it("wishlist nav item has aria-current=page at /wishlist", () => {
    renderSidebar("/wishlist");
    const wishlistLink = screen.getByRole("link", { name: /Wishlist/i });
    expect(wishlistLink).toHaveAttribute("aria-current", "page");
  });

  it("active nav item shows accent dot", () => {
    const { container } = renderSidebar("/stores");
    expect(container.querySelector(".nav-dot")).toBeInTheDocument();
  });

  it("inactive nav items have no accent dot", () => {
    renderSidebar("/stores");
    const dots = document.querySelectorAll(".nav-dot");
    expect(dots).toHaveLength(1);
  });

  it("redemptions nav item has aria-current=page at /redemptions", () => {
    renderSidebar("/redemptions");
    const link = screen.getByRole("link", { name: /Redemptions/i });
    expect(link).toHaveAttribute("aria-current", "page");
  });

  describe("with View Transitions", () => {
    const doc = document as unknown as {
      startViewTransition?: (cb: () => void) => { finished: Promise<void> };
    };

    beforeEach(() => {
      doc.startViewTransition = vi.fn((cb: () => void) => {
        cb();
        return { finished: Promise.resolve() };
      });
      window.matchMedia = vi.fn().mockReturnValue({ matches: false });
    });

    afterEach(() => {
      delete doc.startViewTransition;
      // @ts-expect-error jsdom ships no matchMedia; the test added one and takes it away again.
      delete window.matchMedia;
    });

    it("navigates inside a view transition", async () => {
      renderSidebar("/stores");
      await userEvent.click(screen.getByRole("link", { name: /Wishlist/i }));

      expect(doc.startViewTransition).toHaveBeenCalledOnce();
      expect(screen.getByRole("link", { name: /Wishlist/i })).toHaveAttribute(
        "aria-current",
        "page",
      );
    });

    it("leaves a modified click to the browser", async () => {
      renderSidebar("/stores");
      const user = userEvent.setup();
      await user.keyboard("{Control>}");
      await user.click(screen.getByRole("link", { name: /Wishlist/i }));
      await user.keyboard("{/Control}");

      expect(doc.startViewTransition).not.toHaveBeenCalled();
    });

    it("does not animate a click on the current section", async () => {
      renderSidebar("/stores");
      await userEvent.click(screen.getByRole("link", { name: /Stores/i }));
      expect(doc.startViewTransition).not.toHaveBeenCalled();
    });
  });

  it("renders its footer below the navigation", () => {
    render(
      <MemoryRouter>
        <Sidebar footer={<a href="/">Footer link</a>} />
      </MemoryRouter>,
    );
    expect(
      screen.getByRole("link", { name: "Footer link" }).parentElement,
    ).toHaveClass("sidebar-foot");
  });

  it("nav items have zero-padded numbers", () => {
    renderSidebar("/");
    expect(screen.getByText("01")).toBeInTheDocument();
    expect(screen.getByText("02")).toBeInTheDocument();
    expect(screen.getByText("03")).toBeInTheDocument();
    expect(screen.getByText("04")).toBeInTheDocument();
  });
});
