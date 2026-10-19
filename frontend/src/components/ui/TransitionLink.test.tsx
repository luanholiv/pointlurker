import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import TransitionLink from "./TransitionLink";

const doc = document as unknown as {
  startViewTransition?: (cb: () => void) => { finished: Promise<void> };
};

const renderAt = (path: string, to: string) =>
  render(
    <MemoryRouter initialEntries={[path]}>
      <TransitionLink to={to}>Go</TransitionLink>
      <Routes>
        <Route path="/" element={<h1>Home</h1>} />
        <Route path="/settings" element={<h1>Settings</h1>} />
      </Routes>
    </MemoryRouter>,
  );

describe("TransitionLink", () => {
  beforeEach(() => {
    doc.startViewTransition = vi.fn((cb: () => void) => {
      cb();
      return { finished: Promise.resolve() };
    });
  });

  afterEach(() => {
    delete doc.startViewTransition;
  });

  it("navigates inside a view transition, hash included", async () => {
    renderAt("/", "/settings#token");
    await userEvent.click(screen.getByRole("link", { name: "Go" }));

    expect(doc.startViewTransition).toHaveBeenCalledOnce();
    expect(
      screen.getByRole("heading", { name: "Settings" }),
    ).toBeInTheDocument();
  });

  it("leaves a modified click to the browser", async () => {
    renderAt("/", "/settings");
    const user = userEvent.setup();
    await user.keyboard("{Control>}");
    await user.click(screen.getByRole("link", { name: "Go" }));
    await user.keyboard("{/Control}");

    expect(doc.startViewTransition).not.toHaveBeenCalled();
  });

  it("does not animate a link to the page already shown", async () => {
    renderAt("/", "/");
    await userEvent.click(screen.getByRole("link", { name: "Go" }));
    expect(doc.startViewTransition).not.toHaveBeenCalled();
  });
});
