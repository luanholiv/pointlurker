import { createContext, useContext } from "react";
import type {
  BootstrapState,
  CurrentUserResponse,
  LoginRequest,
  TwoFactorEnrollmentResult,
  TwoFactorSetupResponse,
} from "./authApi";
import type { StoreProvider } from "../stores/storeProviders";

export type AuthUser = CurrentUserResponse;

export type LoginResult = {
  requiresTwoFactor: boolean;
};

export type AuthContextValue = {
  bootstrapState: BootstrapState | null;
  bootstrapStatus: "loading" | "ready";
  user: AuthUser | null;
  isAuthenticated: boolean;
  isUserLoading: boolean;
  login: (payload: LoginRequest) => Promise<LoginResult>;
  register: (payload: { email: string; password: string }) => Promise<void>;
  enableTwoFactor: () => Promise<TwoFactorSetupResponse>;
  verifyTwoFactorEnrollment: (
    code: string,
  ) => Promise<TwoFactorEnrollmentResult>;
  disableTwoFactor: () => Promise<void>;
  changePassword: (payload: {
    currentPassword: string;
    newPassword: string;
  }) => Promise<void>;
  setStreamElementsToken: (token: string) => Promise<void>;
  clearStreamElementsToken: () => Promise<void>;
  /** Stores a provider's credential, then refreshes the user so its summary is current. */
  setProviderCredential: (
    provider: StoreProvider,
    secret: string,
  ) => Promise<void>;
  clearProviderCredential: (provider: StoreProvider) => Promise<void>;
  /** PATCH /api/users/me/locale — persists locale and calls i18next.changeLanguage */
  changeLocale: (locale: string) => Promise<void>;
  logout: () => void;
};

export const AuthContext = createContext<AuthContextValue | null>(null);

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used within AuthProvider");
  }

  return context;
};
