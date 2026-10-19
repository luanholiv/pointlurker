import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { renderWithToasts } from "../../test/renderWithToasts";
import {
  AuthContext,
  type AuthContextValue,
} from "../../features/auth/AuthContext";
import StoresPage from "./StoresPage";

vi.mock("../../features/stores/useStores", () => ({
  useStoreList: vi.fn(),
  useCreateStore: () => ({
    execute: vi.fn(),
    status: "idle",
    errorMessage: "",
  }),
  useStoreLookup: () => ({
    state: { status: "idle" },
    lookup: vi.fn(),
    reset: vi.fn(),
  }),
  useActivateStore: vi.fn(),
  useDeactivateStore: vi.fn(),
  useRemoveStore: vi.fn(),
}));

import {
  useStoreList,
  useActivateStore,
  useDeactivateStore,
  useRemoveStore,
} from "../../features/stores/useStores";
const mockUseStoreList = vi.mocked(useStoreList);
const mockUseActivateStore = vi.mocked(useActivateStore);
const mockUseDeactivateStore = vi.mocked(useDeactivateStore);
const mockUseRemoveStore = vi.mocked(useRemoveStore);

const mockAuth = (
  overrides: Partial<AuthContextValue> = {},
): AuthContextValue => ({
  bootstrapState: { userExists: true },
  bootstrapStatus: "ready",
  user: {
    userId: "1",
    email: "test@test.com",
    credentials: [
      {
        provider: "streamelements",
        accountName: null,
        expiresAt: null,
        lastValidatedAt: null,
        lastValidationError: null,
      },
    ],
    twoFactorEnabled: false,
    locale: "en-US",
  },
  isAuthenticated: true,
  isUserLoading: false,
  login: vi.fn(),
  register: vi.fn(),
  enableTwoFactor: vi.fn(),
  verifyTwoFactorEnrollment: vi.fn(),
  disableTwoFactor: vi.fn(),
  changePassword: vi.fn(),
  setStreamElementsToken: vi.fn(),
  clearStreamElementsToken: vi.fn(),
  setProviderCredential: vi.fn(),
  clearProviderCredential: vi.fn(),
  changeLocale: vi.fn(),
  logout: vi.fn(),
  ...overrides,
});

const renderWithProviders = (authOverrides: Partial<AuthContextValue> = {}) =>
  renderWithToasts(
    <AuthContext.Provider value={mockAuth(authOverrides)}>
      <MemoryRouter>
        <StoresPage />
      </MemoryRouter>
    </AuthContext.Provider>,
  );

const activeStore = {
  id: "s1",
  channelId: "ch1",
  displayName: "Active Store",
  isActive: true,
  lastSyncedAt: null,
  itemCount: 0,
  provider: "streamelements" as const,
};

const pausedStore = {
  id: "s2",
  channelId: "ch2",
  displayName: "Paused Store",
  isActive: false,
  lastSyncedAt: null,
  itemCount: 0,
  provider: "streamelements" as const,
};

/**
 * The list hook reports the filtered page and, separately, the totals across every owned store.
 * Only the parts a case is about are overridden.
 */
const listDefaults = {
  totalCount: 0,
  summary: { activeCount: 0, pausedCount: 0 },
};

describe("StoresPage", () => {
  beforeEach(() => {
    mockUseActivateStore.mockReturnValue({ execute: vi.fn(), status: "idle" });
    mockUseDeactivateStore.mockReturnValue({
      execute: vi.fn(),
      status: "idle",
    });
    mockUseRemoveStore.mockReturnValue({ execute: vi.fn(), status: "idle" });
  });

  it("shows loading state", () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [],
      status: "loading",
      reload: vi.fn(),
    });
    renderWithProviders();
    expect(screen.getByText("Loading stores...")).toBeInTheDocument();
  });

  it("shows empty state when no stores", () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    expect(
      screen.getByRole("heading", { name: "No stores yet" }),
    ).toBeInTheDocument();
  });

  it("first-run empty state offers the add-store dialog", async () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    const [, emptyStateAdd] = screen.getAllByRole("button", {
      name: "Add Store",
    });
    await userEvent.click(emptyStateAdd);
    expect(screen.getByRole("dialog")).toBeInTheDocument();
  });

  it("first-run empty state points to Settings when no credential is held", () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders({
      user: { ...mockAuth().user!, credentials: [] },
    });
    expect(screen.getByRole("link", { name: "Open Settings" })).toHaveAttribute(
      "href",
      "/settings",
    );
  });

  it("filtered empty state clears the search and status filter", async () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      summary: { activeCount: 1, pausedCount: 1 },
      stores: [],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    await userEvent.click(screen.getByRole("button", { name: /search/i }));
    await userEvent.type(
      screen.getByPlaceholderText("Search stores by name or ID\u2026"),
      "zzz",
    );
    await userEvent.click(screen.getByRole("button", { name: "Paused" }));
    expect(
      screen.getByRole("heading", { name: "No stores match" }),
    ).toBeInTheDocument();

    await userEvent.click(
      screen.getByRole("button", { name: "Clear filters" }),
    );

    expect(mockUseStoreList).toHaveBeenLastCalledWith({
      search: "",
      active: null,
    });
  });

  it("renders store cards when stores exist", () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [
        {
          id: "1",
          channelId: "ch1",
          displayName: "Test Store",
          isActive: true,
          lastSyncedAt: "2026-03-01T00:00:00Z",
          itemCount: 5,
          provider: "streamelements" as const,
        },
      ],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    expect(screen.getByText("Test Store")).toBeInTheDocument();
    expect(screen.getByText("5 items")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("shows Add Store button when user is authenticated", () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    expect(
      screen.getAllByRole("button", { name: "Add Store" })[0],
    ).toBeInTheDocument();
  });

  it("shows the credential warning when the user has no credential", () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders({
      user: {
        userId: "1",
        email: "test@test.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    expect(
      screen.getByText("Add a store credential in Settings to add stores."),
    ).toBeInTheDocument();
  });

  it("search toggle expands filter panel", async () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    expect(
      screen.queryByPlaceholderText("Search stores by name or ID…"),
    ).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /search/i }));
    expect(
      screen.getByPlaceholderText("Search stores by name or ID…"),
    ).toBeInTheDocument();
  });

  /**
   * The list is narrowed by the server now, so picking a chip is a *request*, not a local filter.
   * Asserting that the page hides a row would assert a rule it does not own.
   */
  it("status chip asks the server for the narrowed list", async () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [activeStore, pausedStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();

    await userEvent.click(screen.getByRole("button", { name: /search/i }));
    await userEvent.click(screen.getByRole("button", { name: "Paused" }));

    expect(mockUseStoreList).toHaveBeenLastCalledWith(
      expect.objectContaining({ active: false }),
    );
  });

  it("renders the rows the server returned", () => {
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [activeStore, pausedStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();

    expect(screen.getByText("Active Store")).toBeInTheDocument();
    expect(screen.getByText("Paused Store")).toBeInTheDocument();
  });

  it("shows 'Store paused' toast after pausing active store", async () => {
    const mockExecute = vi.fn().mockResolvedValue(true);
    mockUseDeactivateStore.mockReturnValue({
      execute: mockExecute,
      status: "idle",
    });
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [activeStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    await userEvent.click(screen.getByRole("button", { name: /pause/i }));
    expect(await screen.findByText("Store paused")).toBeInTheDocument();
  });

  it("shows 'Store resumed' toast after resuming paused store", async () => {
    const mockExecute = vi.fn().mockResolvedValue(true);
    mockUseActivateStore.mockReturnValue({
      execute: mockExecute,
      status: "idle",
    });
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [pausedStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    await userEvent.click(screen.getByRole("button", { name: /resume/i }));
    expect(await screen.findByText("Store resumed")).toBeInTheDocument();
  });

  it("resume calls activate, not deactivate", async () => {
    const activateExecute = vi.fn().mockResolvedValue(true);
    const deactivateExecute = vi.fn().mockResolvedValue(true);
    mockUseActivateStore.mockReturnValue({
      execute: activateExecute,
      status: "idle",
    });
    mockUseDeactivateStore.mockReturnValue({
      execute: deactivateExecute,
      status: "idle",
    });
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [pausedStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    await userEvent.click(screen.getByRole("button", { name: /resume/i }));
    expect(activateExecute).toHaveBeenCalledWith(pausedStore.id);
    expect(deactivateExecute).not.toHaveBeenCalled();
  });

  it("shows error toast when toggle fails", async () => {
    const mockExecute = vi.fn().mockResolvedValue(false);
    mockUseDeactivateStore.mockReturnValue({
      execute: mockExecute,
      status: "idle",
    });
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [activeStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    await userEvent.click(screen.getByRole("button", { name: /pause/i }));
    expect(
      await screen.findByText("Failed to update store status"),
    ).toBeInTheDocument();
  });

  it("shows 'Store removed' toast after successful remove", async () => {
    const mockExecute = vi.fn().mockResolvedValue(true);
    mockUseRemoveStore.mockReturnValue({
      execute: mockExecute,
      status: "idle",
    });
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [activeStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    await userEvent.click(screen.getByRole("button", { name: /remove/i }));
    expect(await screen.findByText("Store removed")).toBeInTheDocument();
  });

  it("shows error toast when remove fails", async () => {
    const mockExecute = vi.fn().mockResolvedValue(false);
    mockUseRemoveStore.mockReturnValue({
      execute: mockExecute,
      status: "idle",
    });
    mockUseStoreList.mockReturnValue({
      ...listDefaults,
      stores: [activeStore],
      status: "ready",
      reload: vi.fn(),
    });
    renderWithProviders();
    await userEvent.click(screen.getByRole("button", { name: /remove/i }));
    const toast = await screen.findByText("Failed to remove store");
    expect(toast).toBeInTheDocument();
    expect(toast.closest("[data-tone]")).toHaveAttribute("data-tone", "error");
  });
});
