import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { describe, expect, it } from "vitest";
import {
  AuthContext,
  type AuthContextValue,
} from "../../features/auth/AuthContext";
import { AuthGate } from "./AuthGate";

const baseAuthState: AuthContextValue = {
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
};

describe("AuthGate", () => {
  it("renders loading screen when bootstrapStatus is ready and isUserLoading is true", () => {
    render(
      <AuthContext.Provider value={{ ...baseAuthState, isUserLoading: true }}>
        <MemoryRouter>
          <AuthGate>
            <div>Protected content</div>
          </AuthGate>
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(
      screen.getByText("Loading authentication status..."),
    ).toBeInTheDocument();
    expect(screen.queryByText("Protected content")).not.toBeInTheDocument();
  });

  it("renders children when bootstrapStatus is ready, isUserLoading is false, and isAuthenticated is true", () => {
    render(
      <AuthContext.Provider
        value={{
          ...baseAuthState,
          isUserLoading: false,
          isAuthenticated: true,
          user: {
            userId: "user-1",
            email: "admin@example.com",
            credentials: [],
            twoFactorEnabled: false,
            locale: "en-US",
          },
        }}
      >
        <MemoryRouter>
          <AuthGate>
            <div>Protected content</div>
          </AuthGate>
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(screen.getByText("Protected content")).toBeInTheDocument();
  });

  it("navigates to /login when bootstrapStatus is ready, isUserLoading is false, and isAuthenticated is false", () => {
    render(
      <AuthContext.Provider
        value={{
          ...baseAuthState,
          isUserLoading: false,
          isAuthenticated: false,
        }}
      >
        <MemoryRouter initialEntries={["/stores"]}>
          <AuthGate>
            <div>Protected content</div>
          </AuthGate>
        </MemoryRouter>
      </AuthContext.Provider>,
    );

    expect(screen.queryByText("Protected content")).not.toBeInTheDocument();
  });
});
