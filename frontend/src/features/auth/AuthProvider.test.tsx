import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it, vi, beforeEach } from "vitest";
import { useAuth } from "./AuthContext";
import { AuthProvider } from "./AuthProvider";

vi.mock("./authApi", () => ({
  fetchBootstrapState: vi.fn(),
  fetchCurrentUser: vi.fn(),
  loginUser: vi.fn(),
  registerUser: vi.fn(),
  refreshSession: vi.fn(),
  enableTwoFactor: vi.fn(),
  verifyTwoFactorEnrollment: vi.fn(),
  disableTwoFactor: vi.fn(),
  changePassword: vi.fn(),
  setUserLocale: vi.fn(),
  setStreamElementsToken: vi.fn(),
  clearStreamElementsToken: vi.fn(),
  setProviderCredential: vi.fn(),
  clearProviderCredential: vi.fn(),
}));

vi.mock("./authStorage", () => ({
  getTokenSet: vi.fn(),
  setTokenSet: vi.fn(),
  clearTokenSet: vi.fn(),
}));

import {
  disableTwoFactor,
  fetchBootstrapState,
  fetchCurrentUser,
  loginUser,
  refreshSession,
  setUserLocale,
  verifyTwoFactorEnrollment,
} from "./authApi";
import { getTokenSet, clearTokenSet, setTokenSet } from "./authStorage";

const mockedGetTokenSet = vi.mocked(getTokenSet);
const mockedSetTokenSet = vi.mocked(setTokenSet);
const mockedClearTokenSet = vi.mocked(clearTokenSet);
const mockedFetchBootstrapState = vi.mocked(fetchBootstrapState);
const mockedFetchCurrentUser = vi.mocked(fetchCurrentUser);
const mockedLoginUser = vi.mocked(loginUser);
const mockedRefreshSession = vi.mocked(refreshSession);
const mockedSetUserLocale = vi.mocked(setUserLocale);
const mockedVerifyTwoFactorEnrollment = vi.mocked(verifyTwoFactorEnrollment);
const mockedDisableTwoFactor = vi.mocked(disableTwoFactor);

// Far-future expiry so the proactive-refresh effect never fires in these tests.
const tokenSet = (accessToken: string) => ({
  accessToken,
  refreshToken: "refresh",
  expiresAt: Date.now() + 3_600_000,
});

const AuthConsumer = () => {
  const { isUserLoading, isAuthenticated, user, logout } = useAuth();
  return (
    <>
      <div data-testid="isUserLoading">{String(isUserLoading)}</div>
      <div data-testid="isAuthenticated">{String(isAuthenticated)}</div>
      <div data-testid="email">{user?.email ?? ""}</div>
      <button onClick={logout}>Logout</button>
    </>
  );
};

beforeEach(() => {
  vi.resetAllMocks();
  mockedFetchBootstrapState.mockResolvedValue({ userExists: true });
});

describe("AuthProvider isUserLoading", () => {
  it("is true when token exists but user is null", () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("token"));
    mockedFetchCurrentUser.mockReturnValue(new Promise(() => {}));

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>,
    );

    expect(screen.getByTestId("isUserLoading")).toHaveTextContent("true");
  });

  it("is false when token and user both exist", async () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("token"));
    mockedFetchCurrentUser.mockResolvedValue({
      userId: "user-1",
      email: "admin@example.com",
      credentials: [],
      twoFactorEnabled: false,
      locale: "en-US",
    });

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("isUserLoading")).toHaveTextContent("false");
    });
  });

  it("is false when no token exists", () => {
    mockedGetTokenSet.mockReturnValue(null);

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>,
    );

    expect(screen.getByTestId("isUserLoading")).toHaveTextContent("false");
  });
});

describe("AuthProvider fetchCurrentUser failure", () => {
  it("clears the token set when fetchCurrentUser rejects", async () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("bad-token"));
    mockedFetchCurrentUser.mockRejectedValue(new Error("401 Unauthorized"));

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("isUserLoading")).toHaveTextContent("false");
    });
    expect(mockedClearTokenSet).toHaveBeenCalled();
  });
});

describe("AuthProvider logout", () => {
  it("clears storage and user on logout", async () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("valid-token"));
    mockedFetchCurrentUser.mockResolvedValue({
      userId: "u-1",
      email: "a@b.com",
      credentials: [],
      twoFactorEnabled: false,
      locale: "en-US",
    });
    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("email")).toHaveTextContent("a@b.com");
    });

    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Logout" }));

    await waitFor(() => {
      expect(screen.getByTestId("isAuthenticated")).toHaveTextContent("false");
    });
    expect(mockedClearTokenSet).toHaveBeenCalled();
  });
});

describe("AuthProvider login", () => {
  it("stores the full token set on successful login", async () => {
    mockedGetTokenSet.mockReturnValue(null);
    mockedLoginUser.mockResolvedValue({
      token: {
        accessToken: "new-token",
        refreshToken: "ref",
        tokenType: "Bearer",
        expiresIn: 3600,
      },
    });
    mockedFetchCurrentUser.mockResolvedValue({
      userId: "u-1",
      email: "loggedin@example.com",
      credentials: [],
      twoFactorEnabled: false,
      locale: "en-US",
    });

    const LoginConsumer = () => {
      const { login, isAuthenticated } = useAuth();
      return (
        <>
          <div data-testid="isAuthenticated">{String(isAuthenticated)}</div>
          <button
            onClick={() => login({ email: "a@b.com", password: "P@ss1" })}
          >
            Login
          </button>
        </>
      );
    };

    render(
      <AuthProvider>
        <LoginConsumer />
      </AuthProvider>,
    );

    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Login" }));

    await waitFor(() => {
      expect(screen.getByTestId("isAuthenticated")).toHaveTextContent("true");
    });
    expect(mockedSetTokenSet).toHaveBeenCalledWith(
      expect.objectContaining({
        accessToken: "new-token",
        refreshToken: "ref",
      }),
    );
  });

  it("returns requiresTwoFactor:true for 2FA challenge", async () => {
    mockedGetTokenSet.mockReturnValue(null);
    mockedLoginUser.mockResolvedValue({
      challenge: { requiresTwoFactor: true },
    });

    let loginResult: { requiresTwoFactor: boolean } | null = null;
    const TwoFaConsumer = () => {
      const { login } = useAuth();
      return (
        <button
          onClick={async () => {
            loginResult = await login({ email: "a@b.com", password: "P@ss1" });
          }}
        >
          Login
        </button>
      );
    };

    render(
      <AuthProvider>
        <TwoFaConsumer />
      </AuthProvider>,
    );

    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Login" }));

    await waitFor(() => {
      expect(loginResult?.requiresTwoFactor).toBe(true);
    });
  });
});

describe("AuthProvider changeLocale", () => {
  it("calls setUserLocale API, refreshes user, and updates locale", async () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("valid-token"));
    mockedFetchCurrentUser
      .mockResolvedValueOnce({
        userId: "u-1",
        email: "a@b.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "en-US",
      })
      .mockResolvedValueOnce({
        userId: "u-1",
        email: "a@b.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "pt-BR",
      });
    mockedSetUserLocale.mockResolvedValue(undefined as never);

    const LocaleConsumer = () => {
      const { user, changeLocale } = useAuth();
      return (
        <>
          <div data-testid="locale">{user?.locale ?? ""}</div>
          <button onClick={() => changeLocale("pt-BR")}>Change locale</button>
        </>
      );
    };

    render(
      <AuthProvider>
        <LocaleConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("locale")).toHaveTextContent("en-US");
    });

    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Change locale" }));

    await waitFor(() => {
      expect(screen.getByTestId("locale")).toHaveTextContent("pt-BR");
    });

    expect(mockedSetUserLocale).toHaveBeenCalledWith("pt-BR");
    expect(mockedFetchCurrentUser).toHaveBeenCalledTimes(2);
  });
});

/**
 * The refetch after each of these is the whole mechanism behind the settings panel reading two-factor
 * state from the server rather than tracking a local flag. Drop either `refreshUser()` and the panel
 * silently renders stale state again, so the call count is the assertion that matters here.
 */
describe("AuthProvider two-factor", () => {
  const profile = (twoFactorEnabled: boolean) => ({
    userId: "u-1",
    email: "a@b.com",
    credentials: [],
    twoFactorEnabled,
    locale: "en-US",
  });

  it("refreshes the profile after enrolment and hands back the recovery codes", async () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("valid-token"));
    mockedFetchCurrentUser
      .mockResolvedValueOnce(profile(false))
      .mockResolvedValueOnce(profile(true));
    mockedVerifyTwoFactorEnrollment.mockResolvedValue({
      recoveryCodes: ["aaaa-1111", "bbbb-2222"],
    });

    let issued: unknown;
    const VerifyConsumer = () => {
      const { user, verifyTwoFactorEnrollment: verify } = useAuth();
      return (
        <>
          <div data-testid="twoFactor">{String(user?.twoFactorEnabled)}</div>
          <button
            onClick={async () => {
              issued = await verify("123456");
            }}
          >
            Verify
          </button>
        </>
      );
    };

    render(
      <AuthProvider>
        <VerifyConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("twoFactor")).toHaveTextContent("false");
    });

    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Verify" }));

    await waitFor(() => {
      expect(screen.getByTestId("twoFactor")).toHaveTextContent("true");
    });

    expect(mockedVerifyTwoFactorEnrollment).toHaveBeenCalledWith("123456");
    expect(mockedFetchCurrentUser).toHaveBeenCalledTimes(2);
    expect(issued).toEqual({ recoveryCodes: ["aaaa-1111", "bbbb-2222"] });
  });

  it("refreshes the profile after disabling", async () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("valid-token"));
    mockedFetchCurrentUser
      .mockResolvedValueOnce(profile(true))
      .mockResolvedValueOnce(profile(false));
    mockedDisableTwoFactor.mockResolvedValue(undefined as never);

    const DisableConsumer = () => {
      const { user, disableTwoFactor: disable } = useAuth();
      return (
        <>
          <div data-testid="twoFactor">{String(user?.twoFactorEnabled)}</div>
          <button onClick={() => void disable()}>Disable</button>
        </>
      );
    };

    render(
      <AuthProvider>
        <DisableConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("twoFactor")).toHaveTextContent("true");
    });

    await userEvent
      .setup()
      .click(screen.getByRole("button", { name: "Disable" }));

    await waitFor(() => {
      expect(screen.getByTestId("twoFactor")).toHaveTextContent("false");
    });

    expect(mockedDisableTwoFactor).toHaveBeenCalledTimes(1);
    expect(mockedFetchCurrentUser).toHaveBeenCalledTimes(2);
  });
});

describe("AuthProvider proactive refresh", () => {
  it("refreshes on mount when the token is at/near expiry", async () => {
    mockedGetTokenSet.mockReturnValue({
      accessToken: "old",
      refreshToken: "r",
      expiresAt: Date.now() + 1_000, // within the ~60s threshold
    });
    mockedFetchCurrentUser.mockResolvedValue({
      userId: "u-1",
      email: "a@b.com",
      credentials: [],
      twoFactorEnabled: false,
      locale: "en-US",
    });
    mockedRefreshSession.mockResolvedValue("new");

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(mockedRefreshSession).toHaveBeenCalled();
    });
  });

  it("does not refresh on mount when the token is comfortably fresh", async () => {
    mockedGetTokenSet.mockReturnValue(tokenSet("fresh"));
    mockedFetchCurrentUser.mockResolvedValue({
      userId: "u-1",
      email: "a@b.com",
      credentials: [],
      twoFactorEnabled: false,
      locale: "en-US",
    });

    render(
      <AuthProvider>
        <AuthConsumer />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByTestId("email")).toHaveTextContent("a@b.com");
    });
    expect(mockedRefreshSession).not.toHaveBeenCalled();
  });
});
