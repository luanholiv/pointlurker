import { render, screen } from "@testing-library/react";
import type { ReactNode } from "react";
import { MemoryRouter } from "react-router";
import ToastProvider from "../../components/ui/ToastProvider";
import SetupProgressWidget from "./SetupProgressWidget";
import { useSetupProgress, type SetupProgress } from "./useSetupProgress";

vi.mock("./useSetupProgress", () => ({ useSetupProgress: vi.fn() }));

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

const Providers = ({ children }: { children: ReactNode }) => (
  <ToastProvider>
    <MemoryRouter initialEntries={["/stores"]}>{children}</MemoryRouter>
  </ToastProvider>
);

const renderWidget = () =>
  render(<SetupProgressWidget />, { wrapper: Providers });

describe("SetupProgressWidget", () => {
  beforeEach(() => localStorage.removeItem("pl.setup.dismissed"));

  it("links to the checklist with the count while setup is open", () => {
    mockProgress.mockReturnValue(ready(1));
    renderWidget();

    const link = screen.getByRole("link", { name: /Finish setup/ });
    expect(link).toHaveAttribute("href", "/");
    expect(link).toHaveTextContent("1/3");
  });

  it("shows a completed setup until it is finished", () => {
    mockProgress.mockReturnValue(ready(3));
    renderWidget();

    const link = screen.getByRole("link", { name: /Setup complete/ });
    expect(link).toHaveAttribute("href", "/");
  });

  it("renders nothing once setup is finished", () => {
    localStorage.setItem("pl.setup.dismissed", "1");
    mockProgress.mockReturnValue(ready(3));
    renderWidget();
    expect(screen.queryByRole("link")).toBeNull();
  });

  it("announces completion when the last required step is done", () => {
    mockProgress.mockReturnValue(ready(2));
    const { rerender } = renderWidget();

    mockProgress.mockReturnValue(ready(3));
    rerender(<SetupProgressWidget />);

    expect(
      screen.getByText("PointLurker is watching your stores."),
    ).toBeInTheDocument();
  });

  it("does not announce a setup that was already complete", () => {
    mockProgress.mockReturnValue(ready(3));
    renderWidget();
    expect(
      screen.queryByText("PointLurker is watching your stores."),
    ).toBeNull();
  });

  it.each(["loading", "error"] as const)(
    "renders nothing while progress is %s",
    (status) => {
      mockProgress.mockReturnValue({ status });
      renderWidget();
      expect(screen.queryByRole("link")).toBeNull();
    },
  );
});
