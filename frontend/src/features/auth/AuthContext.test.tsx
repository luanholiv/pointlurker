import { describe, it, expect } from "vitest";
import { renderHook } from "@testing-library/react";
import { AuthContext, useAuth } from "./AuthContext";
import type { AuthContextValue } from "./AuthContext";
import React from "react";

const makeContextValue = (
  overrides?: Partial<AuthContextValue>,
): AuthContextValue => ({
  bootstrapState: null,
  bootstrapStatus: "loading",
  user: null,
  isAuthenticated: false,
  isUserLoading: false,
  login: async () => ({ requiresTwoFactor: false }),
  register: async () => {},
  enableTwoFactor: async () => ({ sharedKey: "", otpauthUri: "" }),
  verifyTwoFactorEnrollment: async () => ({ recoveryCodes: null }),
  disableTwoFactor: async () => {},
  changePassword: async () => {},
  changeLocale: async () => {},
  setStreamElementsToken: async () => {},
  clearStreamElementsToken: async () => {},
  setProviderCredential: async () => {},
  clearProviderCredential: async () => {},
  logout: () => {},
  ...overrides,
});

describe("AuthContext", () => {
  it("useAuth throws when used outside AuthProvider", () => {
    expect(() => renderHook(() => useAuth())).toThrow(
      "useAuth must be used within AuthProvider",
    );
  });

  it("useAuth returns context value when inside provider", () => {
    const value = makeContextValue({ bootstrapStatus: "ready" });
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
    );
    const { result } = renderHook(() => useAuth(), { wrapper });
    expect(result.current.bootstrapStatus).toBe("ready");
  });

  it("useAuth exposes user, isAuthenticated, and isUserLoading", () => {
    const user = {
      userId: "u-1",
      email: "a@b.com",
      credentials: [
        {
          provider: "streamelements" as const,
          accountName: null,
          expiresAt: null,
          lastValidatedAt: null,
          lastValidationError: null,
        },
      ],
      twoFactorEnabled: false,
      locale: "en-US",
    };
    const value = makeContextValue({
      user,
      isAuthenticated: true,
      isUserLoading: false,
    });
    const wrapper = ({ children }: { children: React.ReactNode }) => (
      <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
    );
    const { result } = renderHook(() => useAuth(), { wrapper });
    expect(result.current.user).toEqual(user);
    expect(result.current.isAuthenticated).toBe(true);
    expect(result.current.isUserLoading).toBe(false);
  });
});
