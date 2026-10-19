import {
  clearTokenSet,
  getAccessToken,
  getTokenSet,
  setTokenSet,
} from "../features/auth/authStorage";

export type ApiClientOptions = {
  baseUrl: string;
  fetcher?: typeof fetch;
};

export type RequestOptions = Omit<RequestInit, "body" | "method">;

/**
 * Structured error extracted from an `application/problem+json` response.
 * Mirrors the RFC 7807 shape augmented with `code` + `params` backend extensions.
 */
export class ApiError extends Error {
  readonly code: string;
  readonly params: Record<string, unknown>;
  readonly status: number;
  readonly title: string;

  constructor(payload: {
    code: string;
    params?: Record<string, unknown>;
    status: number;
    title?: string;
  }) {
    super(payload.title ?? payload.code);
    this.name = "ApiError";
    this.code = payload.code;
    this.params = payload.params ?? {};
    this.status = payload.status;
    this.title = payload.title ?? payload.code;
  }
}

/**
 * Builds the `ApiError` for a problem document that has already been read off a response.
 *
 * `parseError` below does the reading itself, but a caller that has to inspect the body before
 * deciding what the failure means — the login challenge — cannot read it twice. Both paths land
 * here so the shape of an untyped failure is defined once.
 */
export const toApiError = (status: number, problem: unknown): ApiError => {
  const body = (problem ?? {}) as {
    code?: string;
    params?: Record<string, unknown>;
    title?: string;
  };

  return new ApiError({
    code: body.code ?? "system.internal_error",
    params: body.params ?? {},
    status,
    title: body.title,
  });
};

const trimTrailingSlash = (value: string) => value.replace(/\/+$/, "");
const trimLeadingSlash = (value: string) => value.replace(/^\/+/, "");

const buildUrl = (baseUrl: string, path: string) => {
  const normalizedBase = trimTrailingSlash(baseUrl);
  const normalizedPath = trimLeadingSlash(path);
  return `${normalizedBase}/${normalizedPath}`;
};

// Logout signal — AuthProvider subscribes so a failed refresh tears down state.

type LogoutListener = () => void;
const logoutListeners = new Set<LogoutListener>();

/** Subscribe to the logout signal; returns an unsubscribe function. */
export const subscribeLogout = (listener: LogoutListener) => {
  logoutListeners.add(listener);
  return () => {
    logoutListeners.delete(listener);
  };
};

const emitLogout = () => {
  for (const listener of logoutListeners) {
    listener();
  }
};

// Single-flight refresh. Concurrent callers share one in-flight refresh promise.
// `/api/auth/refresh` is always called with raw fetch to avoid recursion.

let inFlightRefresh: Promise<string> | null = null;

const performRefresh = async (
  baseUrl: string,
  fetcher: typeof fetch,
): Promise<string> => {
  const tokenSet = getTokenSet();
  const refreshToken = tokenSet?.refreshToken;

  if (!refreshToken) {
    clearTokenSet();
    emitLogout();
    throw new Error("No refresh token available");
  }

  const response = await fetcher(buildUrl(baseUrl, "/api/auth/refresh"), {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ refreshToken }),
  });

  if (!response.ok) {
    clearTokenSet();
    emitLogout();
    throw new Error(`Refresh failed with status ${response.status}`);
  }

  const data = (await response.json()) as {
    accessToken: string;
    refreshToken: string;
    expiresIn: number;
  };

  const stored = setTokenSet({
    accessToken: data.accessToken,
    refreshToken: data.refreshToken,
    expiresIn: data.expiresIn,
  });

  return stored.accessToken;
};

/**
 * Refreshes the access token at most once concurrently (single-flight). Returns
 * the new access token. On failure clears storage and emits the logout signal.
 */
export const refreshOnce = (
  baseUrl: string,
  fetcher: typeof fetch = fetch,
): Promise<string> => {
  inFlightRefresh ??= performRefresh(baseUrl, fetcher).finally(() => {
    inFlightRefresh = null;
  });
  return inFlightRefresh;
};

/** Test-only: resets the in-flight refresh promise and logout listeners. */
export const __resetRefreshState = () => {
  inFlightRefresh = null;
  logoutListeners.clear();
};

// Request pipeline.

const REFRESH_PATH = "/api/auth/refresh";

const mergeAuthHeader = (
  headers: HeadersInit | undefined,
  token: string | null,
): HeadersInit => {
  const base: Record<string, string> = {
    "Content-Type": "application/json",
    ...(headers as Record<string, string> | undefined),
  };
  // Only inject the bearer when the caller has not already set one.
  if (token && base.Authorization === undefined) {
    base.Authorization = `Bearer ${token}`;
  }
  return base;
};

const parseError = async (response: Response): Promise<never> => {
  const contentType = response.headers.get("Content-Type") ?? "";
  if (contentType.includes("application/problem+json")) {
    try {
      throw toApiError(response.status, await response.json());
    } catch (e) {
      if (e instanceof ApiError) throw e;
      // Parsing failed — fall through to generic error.
    }
  }
  throw new Error(`Request failed with status ${response.status}`);
};

const request = async <T>(
  fetcher: typeof fetch,
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE",
  baseUrl: string,
  path: string,
  options: RequestOptions = {},
  body?: unknown,
): Promise<T> => {
  const url = buildUrl(baseUrl, path);
  const serializedBody = body === undefined ? undefined : JSON.stringify(body);
  const callerHeaders = options.headers;
  const isRefreshCall = path.replace(/\/+$/, "").endsWith(REFRESH_PATH);

  const send = (token: string | null) =>
    fetcher(url, {
      ...options,
      method,
      headers: mergeAuthHeader(callerHeaders, token),
      body: serializedBody,
    });

  let response = await send(getAccessToken());

  // Reactive refresh: a 401 on an authenticated, non-refresh request triggers a
  // single-flight refresh and exactly one retry with the new token.
  if (response.status === 401 && !isRefreshCall && getTokenSet()) {
    try {
      const newToken = await refreshOnce(baseUrl, fetcher);
      response = await send(newToken);
    } catch {
      // Refresh failed — storage cleared + logout emitted in refreshOnce.
      throw new Error("Session expired");
    }
  }

  if (!response.ok) {
    return parseError(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  // Not every success carries a document: the Identity `register` endpoint answers 200 with an
  // empty body, and `json()` on that throws a parse error — a success surfacing as a failure.
  const payload = await response.text();
  return (payload.length === 0 ? undefined : JSON.parse(payload)) as T;
};

export const createApiClient = ({
  baseUrl,
  fetcher = fetch,
}: ApiClientOptions) => ({
  get: <T>(path: string, options?: RequestOptions) =>
    request<T>(fetcher, "GET", baseUrl, path, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(fetcher, "POST", baseUrl, path, options, body),
  put: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(fetcher, "PUT", baseUrl, path, options, body),
  patch: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>(fetcher, "PATCH", baseUrl, path, options, body),
  del: (path: string, options?: RequestOptions) =>
    request<void>(fetcher, "DELETE", baseUrl, path, options),
});
