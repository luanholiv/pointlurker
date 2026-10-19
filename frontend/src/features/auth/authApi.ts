import { getAppConfig } from "../../config";
import type { StoreProvider } from "../stores/storeProviders";
import { createApiClient, refreshOnce, toApiError } from "../../lib/apiClient";

export type BootstrapState = {
  userExists: boolean;
};

export type RegisterRequest = {
  email: string;
  password: string;
};

export type LoginRequest = {
  email: string;
  password: string;
  twoFactorCode?: string | null;
};

export type LoginChallengeResponse = {
  requiresTwoFactor: boolean;
};

/** Built-in Identity `AccessTokenResponse` (login + refresh). */
export type AccessTokenResponse = {
  accessToken: string;
  refreshToken: string;
  tokenType: string;
  /** Seconds until the access token expires. */
  expiresIn: number;
};

export type TwoFactorSetupResponse = {
  sharedKey: string;
  /** Built client-side from the shared key — the API only returns `sharedKey`. */
  otpauthUri: string;
};

export type TwoFactorEnrollmentResult = {
  /**
   * Identity mints these on the enrolment that first turns two-factor on, and returns null on any
   * later one. They are shown once and never retrievable again, so a null here means "keep the
   * codes you already have", not "this account has none".
   */
  recoveryCodes: string[] | null;
};

/**
 * What the owner may know about one stored credential — never the secret. `lastValidationError`
 * is an error code, set when the provider last refused the credential.
 */
export type CredentialSummary = {
  provider: StoreProvider;
  accountName: string | null;
  expiresAt: string | null;
  lastValidatedAt: string | null;
  lastValidationError: string | null;
};

/** Profile aggregate from `GET /api/users/me`. No `roles` field. */
export type CurrentUserResponse = {
  userId: string;
  email: string;
  locale: string;
  twoFactorEnabled: boolean;
  credentials: CredentialSummary[];
};

/**
 * Contract code an Identity `401` carries when the credentials were right but a second factor is
 * still owed. It is the one sign-in outcome the caller has to tell apart, because it continues the
 * flow rather than ending it.
 */
const TWO_FACTOR_REQUIRED_CODE = "auth.two_factor_required";

const getApiBaseUrl = () => getAppConfig().apiBaseUrl;
const getApi = () => createApiClient({ baseUrl: getApiBaseUrl() });

const ISSUER = "PointLurker";

const buildOtpauthUri = (email: string, sharedKey: string) =>
  `otpauth://totp/${encodeURIComponent(ISSUER)}:${encodeURIComponent(email)}` +
  `?secret=${sharedKey}&issuer=${encodeURIComponent(ISSUER)}&digits=6`;

export const fetchBootstrapState = () =>
  getApi().get<BootstrapState>("/api/auth/bootstrap");

/**
 * Registers the sole user. The Identity `register` endpoint returns `200` with an empty body on
 * success and a `400` problem document on failure — carrying the same `{code, params}` extensions as
 * every other endpoint, so it needs no special handling and goes through the shared client.
 */
export const registerUser = (payload: RegisterRequest) =>
  getApi().post<void>("/api/auth/register", payload);

/**
 * Logs in against the Identity API. Returns the token pair on success, or a 2FA challenge when the
 * server answers `401` with the two-factor code.
 *
 * This is the one call that keeps a raw `fetch` rather than going through `apiClient`: the shared
 * client treats a `401` as an expired access token and spends a refresh on it, which is exactly
 * wrong for a sign-in attempt. Every failure still leaves as an `ApiError`, so the page renders the
 * code through the catalog instead of a status line.
 */
export const loginUser = async (
  payload: LoginRequest,
): Promise<
  { token: AccessTokenResponse } | { challenge: LoginChallengeResponse }
> => {
  const response = await fetch(`${getApiBaseUrl()}/api/auth/login`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(payload),
  });

  const data = await response.json().catch(() => null);

  if (!response.ok) {
    const code = (data as { code?: string } | null)?.code;

    if (response.status === 401 && code === TWO_FACTOR_REQUIRED_CODE) {
      return { challenge: { requiresTwoFactor: true } };
    }

    if (code !== undefined) {
      throw toApiError(response.status, data);
    }

    // No code came back, so there is nothing to look up: this message is for a developer reading a
    // console, and the page renders its own fallback key instead.
    throw new Error(`Login failed with status ${response.status}`);
  }

  return { token: data as AccessTokenResponse };
};

/**
 * Exchanges the stored refresh token for a new pair via the single-flight
 * refresh in the API client (raw fetch, never the auth-aware client).
 */
export const refreshSession = () => refreshOnce(getApiBaseUrl());

export const fetchCurrentUser = () =>
  getApi().get<CurrentUserResponse>("/api/users/me");

/**
 * Reads the TOTP shared key from `POST /manage/2fa` and builds the `otpauth://`
 * QR URI client-side (the API only returns `sharedKey`).
 */
export const enableTwoFactor = async (
  email: string,
): Promise<TwoFactorSetupResponse> => {
  const result = await getApi().post<{ sharedKey: string }>(
    "/api/auth/manage/2fa",
    {},
  );
  return {
    sharedKey: result.sharedKey,
    otpauthUri: buildOtpauthUri(email, result.sharedKey),
  };
};

export const verifyTwoFactorEnrollment = async (
  code: string,
): Promise<TwoFactorEnrollmentResult> => {
  const result = await getApi().post<{ recoveryCodes?: string[] | null }>(
    "/api/auth/manage/2fa",
    { enable: true, twoFactorCode: code },
  );
  return { recoveryCodes: result?.recoveryCodes ?? null };
};

/**
 * `resetSharedKey` rides along deliberately. `enable: false` on its own clears the flag but leaves
 * the TOTP secret on the account, so the authenticator entry the owner just revoked still produces
 * valid codes and re-enrolling hands back the same key. Turning two-factor off has to revoke the
 * secret with it — an owner who disables *because* the key leaked would otherwise keep the leak.
 */
export const disableTwoFactor = () =>
  getApi().post<void>("/api/auth/manage/2fa", {
    enable: false,
    resetSharedKey: true,
  });

/** A provider's credential. The backend checks it with the provider before storing it. */
const credentialPath = (provider: StoreProvider) =>
  `/api/users/me/credentials/${provider}`;

export const setProviderCredential = (
  provider: StoreProvider,
  secret: string,
) => getApi().put<void>(credentialPath(provider), { secret });

export const clearProviderCredential = (provider: StoreProvider) =>
  getApi().del(credentialPath(provider));

export const setStreamElementsToken = (token: string) =>
  setProviderCredential("streamelements", token);

export const clearStreamElementsToken = () =>
  clearProviderCredential("streamelements");

/** Changes the password via `POST /manage/info` (`{ oldPassword, newPassword }`). */
export const changePassword = (payload: {
  currentPassword: string;
  newPassword: string;
}) =>
  getApi().post<void>("/api/auth/manage/info", {
    oldPassword: payload.currentPassword,
    newPassword: payload.newPassword,
  });

export const setUserLocale = (locale: string) =>
  getApi().patch<void>("/api/users/me/locale", { locale });
