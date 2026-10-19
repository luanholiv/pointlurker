import { screen } from "@testing-library/react";
import { renderWithToasts } from "../test/renderWithToasts";
import { MemoryRouter } from "react-router";
import { describe, expect, it } from "vitest";
import {
  AuthContext,
  type AuthContextValue,
} from "../features/auth/AuthContext";
import AppRoutes from "./AppRoutes";

const baseAuthState = {
  bootstrapState: { userExists: true },
  bootstrapStatus: "ready",
  user: null,
  isAuthenticated: false,
  isUserLoading: false,
  login: async () => ({ requiresTwoFactor: false }),
  register: async () => undefined,
  enableTwoFactor: async () => ({ sharedKey: "key", otpauthUri: "otpauth" }),
  verifyTwoFactorEnrollment: async () => ({ recoveryCodes: null }),
  disableTwoFactor: async () => undefined,
  changePassword: async () => undefined,
  setStreamElementsToken: async () => undefined,
  clearStreamElementsToken: async () => undefined,
  setProviderCredential: vi.fn(),
  clearProviderCredential: vi.fn(),
  changeLocale: async () => undefined,
  logout: () => undefined,
} satisfies AuthContextValue;

describe("AppRoutes", () => {
  it("redirects unauthenticated users to login", () => {
    renderWithToasts(
      <AuthContext.Provider value={baseAuthState}>
        <MemoryRouter initialEntries={["/"]}>
          <AppRoutes />
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(
      screen.getByRole("heading", { level: 1, name: "Sign in" }),
    ).toBeInTheDocument();
  });

  it("renders bootstrap form when no users exist", () => {
    renderWithToasts(
      <AuthContext.Provider
        value={{ ...baseAuthState, bootstrapState: { userExists: false } }}
      >
        <MemoryRouter initialEntries={["/bootstrap"]}>
          <AppRoutes />
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(
      screen.getByRole("heading", { level: 1, name: /Create admin/i }),
    ).toBeInTheDocument();
  });

  it("redirects bootstrap to login when user exists", () => {
    renderWithToasts(
      <AuthContext.Provider value={baseAuthState}>
        <MemoryRouter initialEntries={["/bootstrap"]}>
          <AppRoutes />
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(
      screen.getByRole("heading", { level: 1, name: "Sign in" }),
    ).toBeInTheDocument();
  });

  it("lands a fresh install on the setup checklist", async () => {
    renderWithToasts(
      <AuthContext.Provider
        value={{
          ...baseAuthState,
          user: {
            userId: "user",
            email: "admin@example.com",
            credentials: [],
            twoFactorEnabled: false,
            locale: "en-US",
          },
          isAuthenticated: true,
        }}
      >
        <MemoryRouter initialEntries={["/"]}>
          <AppRoutes />
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(
      await screen.findByRole("heading", { level: 1, name: "Get started" }),
    ).toBeInTheDocument();
  });

  it("renders settings when authenticated", () => {
    renderWithToasts(
      <AuthContext.Provider
        value={{
          ...baseAuthState,
          user: {
            userId: "user",
            email: "admin@example.com",
            credentials: [],
            twoFactorEnabled: false,
            locale: "en-US",
          },
          isAuthenticated: true,
        }}
      >
        <MemoryRouter initialEntries={["/settings"]}>
          <AppRoutes />
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(
      screen.getByRole("heading", { level: 1, name: "Settings" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Security" })).toBeInTheDocument();
  });
});
