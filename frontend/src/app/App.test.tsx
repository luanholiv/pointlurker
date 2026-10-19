import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import App from "./App";

vi.mock("../features/auth/AuthProvider", () => ({
  AuthProvider: ({ children }: { children: React.ReactNode }) => children,
}));

vi.mock("../features/auth/AuthContext", () => ({
  useAuth: () => ({
    bootstrapState: { userExists: true },
    bootstrapStatus: "ready",
    user: {
      userId: "user",
      email: "admin@example.com",
      credentials: [],
      twoFactorEnabled: false,
    },
    isAuthenticated: true,
    login: async () => ({ requiresTwoFactor: false }),
    register: async () => undefined,
    enableTwoFactor: async () => ({ sharedKey: "key", otpauthUri: "otpauth" }),
    verifyTwoFactorEnrollment: async () => undefined,
    disableTwoFactor: async () => undefined,
    changePassword: async () => undefined,
    setStreamElementsToken: async () => undefined,
    clearStreamElementsToken: async () => undefined,
    setProviderCredential: vi.fn(),
    clearProviderCredential: vi.fn(),
    logout: () => undefined,
  }),
}));

vi.mock("../features/stores/useStores", () => ({
  useStoreList: () => ({
    stores: [],
    totalCount: 0,
    summary: { activeCount: 0, pausedCount: 0 },
    status: "ready",
    reload: vi.fn(),
  }),
  useActivateStore: () => ({ execute: vi.fn() }),
  useDeactivateStore: () => ({ execute: vi.fn() }),
  useRemoveStore: () => ({ execute: vi.fn() }),
  useCreateStore: () => ({
    execute: vi.fn(),
    status: "idle",
    errorMessage: "",
    reset: vi.fn(),
  }),
  useStoreLookup: () => ({
    state: { stage: "idle" },
    lookup: vi.fn(),
    reset: vi.fn(),
  }),
}));

vi.mock("../features/wishlist/useWishlist", () => ({
  useWishlistList: () => ({ data: null, status: "ready", reload: vi.fn() }),
}));

vi.mock("../features/redemptions/useRedemptions", () => ({
  useRedemptions: () => ({
    data: null,
    status: "ready",
    isRefreshing: false,
    reload: vi.fn(),
  }),
}));

describe("App", () => {
  it("boots the app shell on the home route", () => {
    render(<App />);
    // Sidebar brand is always visible in authenticated layout
    expect(screen.getAllByText("PointLurker").length).toBeGreaterThanOrEqual(1);
  });

  // Pages call useToast without rendering a viewport of their own, so the host has to be here.
  // One viewport, not one per page: two stacks would fight for the same screen corner.
  it("mounts exactly one toast viewport for every route", () => {
    render(<App />);
    expect(document.querySelectorAll("ol.toasts")).toHaveLength(1);
  });
});
