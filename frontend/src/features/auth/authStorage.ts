const AUTH_STORAGE_SLOT = "pl.auth.token";

/**
 * Persisted token set. The access token is an opaque ASP.NET Data Protection
 * blob (NOT a JWT) — never decode it. Expiry is tracked solely via `expiresAt`,
 * an absolute epoch in milliseconds (`receiptTime + expiresIn * 1000`).
 */
export type StoredTokenSet = {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
};

/** Shape returned by the Identity API `AccessTokenResponse` (login/refresh). */
export type TokenSetInput = {
  accessToken: string;
  refreshToken: string;
  /** Seconds until the access token expires (from the server). */
  expiresIn: number;
};

const isStoredTokenSet = (value: unknown): value is StoredTokenSet => {
  if (typeof value !== "object" || value === null) {
    return false;
  }
  const candidate = value as Record<string, unknown>;
  return (
    typeof candidate.accessToken === "string" &&
    typeof candidate.refreshToken === "string" &&
    typeof candidate.expiresAt === "number"
  );
};

/** Reads and parses the full persisted token set, or `null` when absent/corrupt. */
export const getTokenSet = (): StoredTokenSet | null => {
  const raw = localStorage.getItem(AUTH_STORAGE_SLOT);
  if (!raw) {
    return null;
  }
  try {
    const parsed: unknown = JSON.parse(raw);
    return isStoredTokenSet(parsed) ? parsed : null;
  } catch {
    return null;
  }
};

/**
 * Persists the full token set, deriving the absolute `expiresAt` from the
 * server's `expiresIn` at receipt time.
 */
export const setTokenSet = ({
  accessToken,
  refreshToken,
  expiresIn,
}: TokenSetInput): StoredTokenSet => {
  const tokenSet: StoredTokenSet = {
    accessToken,
    refreshToken,
    expiresAt: Date.now() + expiresIn * 1000,
  };
  localStorage.setItem(AUTH_STORAGE_SLOT, JSON.stringify(tokenSet));
  return tokenSet;
};

/** Clears all three values (used on logout and refresh failure). */
export const clearTokenSet = () => {
  localStorage.removeItem(AUTH_STORAGE_SLOT);
};

/** Convenience reader for the current bearer access token, or `null`. */
export const getAccessToken = (): string | null =>
  getTokenSet()?.accessToken ?? null;

/**
 * Backward-compatible setter: stores a bare access token, synthesizing a full
 * set (preserving any existing refresh token; default 1 h expiry) so callers
 * that only set an access token still read it back and stay "authenticated".
 */
export const setAccessToken = (token: string) => {
  const existing = getTokenSet();
  const tokenSet: StoredTokenSet = {
    accessToken: token,
    refreshToken: existing?.refreshToken ?? "",
    expiresAt: existing?.expiresAt ?? Date.now() + 3_600_000,
  };
  localStorage.setItem(AUTH_STORAGE_SLOT, JSON.stringify(tokenSet));
};

/** Backward-compatible clear (removes all three values). */
export const clearAccessToken = () => {
  clearTokenSet();
};
