import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import i18n from "../../i18n";
import { subscribeLogout } from "../../lib/apiClient";
import {
  changePassword,
  clearProviderCredential,
  clearStreamElementsToken,
  disableTwoFactor,
  enableTwoFactor,
  fetchBootstrapState,
  fetchCurrentUser,
  loginUser,
  refreshSession,
  registerUser,
  setProviderCredential,
  setStreamElementsToken,
  setUserLocale,
  verifyTwoFactorEnrollment,
  type LoginRequest,
} from "./authApi";
import { clearTokenSet, getTokenSet, setTokenSet } from "./authStorage";
import {
  AuthContext,
  type AuthContextValue,
  type AuthUser,
  type LoginResult,
} from "./AuthContext";
import type { StoreProvider } from "../stores/storeProviders";

type AuthProviderProps = {
  children: ReactNode;
  value?: AuthContextValue;
};

// Refresh proactively when the access token is within this window of expiry (or past it).
const PROACTIVE_REFRESH_THRESHOLD_MS = 60_000;

export const AuthProvider = ({ children, value }: AuthProviderProps) => {
  const [bootstrapState, setBootstrapState] =
    useState<AuthContextValue["bootstrapState"]>(null);
  const bootstrapStatus: AuthContextValue["bootstrapStatus"] = bootstrapState
    ? "ready"
    : "loading";
  const [token, setToken] = useState<string | null>(
    () => getTokenSet()?.accessToken ?? null,
  );
  const [user, setUser] = useState<AuthUser | null>(null);

  useEffect(() => {
    if (value) {
      return;
    }

    let active = true;
    fetchBootstrapState()
      .then((state) => {
        if (!active) {
          return;
        }
        setBootstrapState(state);
      })
      .catch(() => {
        if (!active) {
          return;
        }
        setBootstrapState({ userExists: true });
      });

    return () => {
      active = false;
    };
  }, [value]);

  useEffect(() => {
    if (value || !token) {
      return;
    }

    let active = true;
    fetchCurrentUser()
      .then((currentUser) => {
        if (active) {
          setUser(currentUser);
          // Sync i18next to the stored locale without a page reload
          void i18n.changeLanguage(currentUser.locale);
        }
      })
      .catch(() => {
        if (active) {
          clearTokenSet();
          setToken(null);
          setUser(null);
        }
      });

    return () => {
      active = false;
    };
  }, [token, value]);

  // Proactive refresh: on mount and whenever the tab becomes visible, refresh the access
  // token if it is at/near expiry. Does not rely on a single long timer (unreliable across
  // tab sleep / device suspend). The reactive 401 path in apiClient is the safety net.
  useEffect(() => {
    if (value) {
      return;
    }

    const maybeRefresh = () => {
      const set = getTokenSet();
      if (!set) {
        return;
      }
      if (set.expiresAt - Date.now() <= PROACTIVE_REFRESH_THRESHOLD_MS) {
        void refreshSession().catch(() => {
          // Refresh failure clears storage + emits the logout signal (see apiClient).
        });
      }
    };

    maybeRefresh();

    const onVisibilityChange = () => {
      if (document.visibilityState === "visible") {
        maybeRefresh();
      }
    };

    document.addEventListener("visibilitychange", onVisibilityChange);
    return () => {
      document.removeEventListener("visibilitychange", onVisibilityChange);
    };
  }, [value]);

  // A failed refresh (in apiClient) clears storage and emits a logout signal; tear down
  // the in-memory auth state in response.
  useEffect(() => {
    if (value) {
      return;
    }
    return subscribeLogout(() => {
      setToken(null);
      setUser(null);
    });
  }, [value]);

  const login = useCallback(
    async (payload: LoginRequest): Promise<LoginResult> => {
      const result = await loginUser(payload);
      if ("challenge" in result) {
        return { requiresTwoFactor: true };
      }

      setTokenSet(result.token);
      setToken(result.token.accessToken);
      const currentUser = await fetchCurrentUser();
      setUser(currentUser);
      void i18n.changeLanguage(currentUser.locale);

      return { requiresTwoFactor: false };
    },
    [],
  );

  const register = useCallback(
    async (payload: { email: string; password: string }) => {
      await registerUser(payload);
      setBootstrapState({ userExists: true });
    },
    [],
  );

  const refreshUser = useCallback(async () => {
    const currentUser = await fetchCurrentUser();
    setUser(currentUser);
  }, []);

  const handleEnableTwoFactor = useCallback(async () => {
    return enableTwoFactor(user?.email ?? "");
  }, [user?.email]);

  // Both of these move `user.twoFactorEnabled`, which is what the settings panel renders from —
  // so each refetches the profile rather than leaving the page to track the flag itself.
  const handleVerifyTwoFactorEnrollment = useCallback(
    async (code: string) => {
      const result = await verifyTwoFactorEnrollment(code);
      await refreshUser();
      return result;
    },
    [refreshUser],
  );

  const handleDisableTwoFactor = useCallback(async () => {
    await disableTwoFactor();
    await refreshUser();
  }, [refreshUser]);

  const handleChangePassword = useCallback(
    async (payload: { currentPassword: string; newPassword: string }) => {
      await changePassword(payload);
    },
    [],
  );

  const handleChangeLocale = useCallback(async (locale: string) => {
    await setUserLocale(locale);
    // Refresh the stored user object so user.locale reflects the new value
    const currentUser = await fetchCurrentUser();
    setUser(currentUser);
    // Hot-swap — no page reload required
    await i18n.changeLanguage(locale);
  }, []);

  const handleSetStreamElementsToken = useCallback(
    async (seToken: string) => {
      await setStreamElementsToken(seToken);
      await refreshUser();
    },
    [refreshUser],
  );

  const handleClearStreamElementsToken = useCallback(async () => {
    await clearStreamElementsToken();
    await refreshUser();
  }, [refreshUser]);

  const handleSetProviderCredential = useCallback(
    async (provider: StoreProvider, secret: string) => {
      await setProviderCredential(provider, secret);
      await refreshUser();
    },
    [refreshUser],
  );

  const handleClearProviderCredential = useCallback(
    async (provider: StoreProvider) => {
      await clearProviderCredential(provider);
      await refreshUser();
    },
    [refreshUser],
  );

  const logout = useCallback(() => {
    clearTokenSet();
    setToken(null);
    setUser(null);
  }, []);

  const isUserLoading = Boolean(token && !user);

  const contextValue = useMemo<AuthContextValue>(
    () => ({
      bootstrapState,
      bootstrapStatus,
      user,
      isAuthenticated: Boolean(user && token),
      isUserLoading,
      login,
      register,
      enableTwoFactor: handleEnableTwoFactor,
      verifyTwoFactorEnrollment: handleVerifyTwoFactorEnrollment,
      disableTwoFactor: handleDisableTwoFactor,
      changePassword: handleChangePassword,
      setStreamElementsToken: handleSetStreamElementsToken,
      clearStreamElementsToken: handleClearStreamElementsToken,
      setProviderCredential: handleSetProviderCredential,
      clearProviderCredential: handleClearProviderCredential,
      changeLocale: handleChangeLocale,
      logout,
    }),
    [
      bootstrapState,
      bootstrapStatus,
      user,
      isUserLoading,
      token,
      login,
      register,
      handleEnableTwoFactor,
      handleVerifyTwoFactorEnrollment,
      handleDisableTwoFactor,
      handleChangePassword,
      handleSetStreamElementsToken,
      handleClearStreamElementsToken,
      handleSetProviderCredential,
      handleClearProviderCredential,
      handleChangeLocale,
      logout,
    ],
  );

  if (value) {
    return (
      <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
    );
  }

  return (
    <AuthContext.Provider value={contextValue}>{children}</AuthContext.Provider>
  );
};
