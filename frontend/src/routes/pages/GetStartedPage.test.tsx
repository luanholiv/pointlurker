import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router";
import {
  useSetupProgress,
  type SetupProgress,
} from "../../features/onboarding/useSetupProgress";
import GetStartedPage from "./GetStartedPage";

vi.mock("../../features/onboarding/useSetupProgress", () => ({
  useSetupProgress: vi.fn(),
}));

const mockProgress = vi.mocked(useSetupProgress);

const ready = (done: number): SetupProgress => ({
  status: "ready",
  steps: [
    { id: "credential", done: done > 0 },
    { id: "store", done: done > 1 },
    { id: "wishlist", done: done > 2 },
  ],
  done,
  total: 3,
  isComplete: done === 3,
  autoRedeem: false,
});

const renderPage = () =>
  render(
    <MemoryRouter initialEntries={["/"]}>
      <Routes>
        <Route index element={<GetStartedPage />} />
        <Route path="/stores" element={<h1>Stores page</h1>} />
      </Routes>
    </MemoryRouter>,
  );

describe("GetStartedPage", () => {
  beforeEach(() => localStorage.removeItem("pl.setup.dismissed"));

  it("shows the checklist while setup is incomplete", () => {
    mockProgress.mockReturnValue(ready(0));
    renderPage();

    expect(
      screen.getByRole("heading", { level: 1, name: "Get started" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("link", { name: "Open credentials" }),
    ).toBeInTheDocument();
  });

  it("stays on a completed checklist until it is finished", () => {
    mockProgress.mockReturnValue(ready(3));
    renderPage();

    expect(
      screen.getByRole("heading", { name: "You're all set" }),
    ).toBeInTheDocument();
  });

  it("goes to the stores page for good once setup is finished", async () => {
    mockProgress.mockReturnValue(ready(3));
    renderPage();

    await userEvent.click(screen.getByRole("button", { name: "Finish setup" }));

    expect(
      await screen.findByRole("heading", { name: "Stores page" }),
    ).toBeInTheDocument();
    expect(localStorage.getItem("pl.setup.dismissed")).toBe("1");
  });

  it("sends a finished setup to the stores page", () => {
    localStorage.setItem("pl.setup.dismissed", "1");
    mockProgress.mockReturnValue(ready(3));
    renderPage();

    expect(
      screen.getByRole("heading", { name: "Stores page" }),
    ).toBeInTheDocument();
  });

  it("falls back to the stores page when progress cannot be read", () => {
    mockProgress.mockReturnValue({ status: "error" });
    renderPage();

    expect(
      screen.getByRole("heading", { name: "Stores page" }),
    ).toBeInTheDocument();
  });

  it("renders nothing while progress loads", () => {
    mockProgress.mockReturnValue({ status: "loading" });
    const { container } = renderPage();
    expect(container).toBeEmptyDOMElement();
  });
});
