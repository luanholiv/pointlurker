import { screen } from "@testing-library/react";
import { renderWithToasts } from "../test/renderWithToasts";
import { MemoryRouter, Route, Routes } from "react-router";
import {
  AuthContext,
  type AuthContextValue,
} from "../features/auth/AuthContext";
import AppLayout from "./AppLayout";

const auth = {
  user: {
    userId: "user",
    email: "admin@example.com",
    credentials: [],
    twoFactorEnabled: false,
    locale: "en-US",
  },
} as unknown as AuthContextValue;

const renderLayout = (path: string) =>
  renderWithToasts(
    <AuthContext.Provider value={auth}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route element={<AppLayout />}>
            <Route index element={<div>Dashboard content</div>} />
            <Route path="/stores" element={<div>Stores content</div>} />
            <Route
              path="/stores/:id"
              element={<div>Store detail content</div>}
            />
            <Route path="/wishlist" element={<div>Wishlist content</div>} />
            <Route path="/settings" element={<div>Settings content</div>} />
          </Route>
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );

describe("AppLayout", () => {
  it("renders sidebar with brand", () => {
    renderLayout("/");
    // "PointLurker" appears in sidebar brand AND topbar breadcrumbs
    expect(screen.getAllByText("PointLurker").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Console")).toBeInTheDocument();
  });

  it("renders nav items", () => {
    renderLayout("/");
    expect(screen.getAllByText("Stores").length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText("Wishlist")).toBeInTheDocument();
    expect(screen.getByText("Settings")).toBeInTheDocument();
  });

  it("home breadcrumb shows Get started", () => {
    renderLayout("/");
    expect(screen.getByText("Get started")).toBeInTheDocument();
  });

  it("shows setup progress in the sidebar", async () => {
    renderLayout("/stores");
    expect(
      await screen.findByRole("link", { name: /Finish setup/ }),
    ).toHaveTextContent("0/3");
  });

  it("stores breadcrumb shows Stores", () => {
    renderLayout("/stores");
    expect(screen.getAllByText("Stores").length).toBeGreaterThanOrEqual(1);
  });

  it("renders outlet content", () => {
    renderLayout("/");
    expect(screen.getByText("Dashboard content")).toBeInTheDocument();
  });
});
