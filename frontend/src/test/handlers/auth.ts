import { http, HttpResponse } from "msw";

const BASE = "http://localhost";

const accessTokenResponse = {
  accessToken: "test-access-token",
  refreshToken: "test-refresh-token",
  tokenType: "Bearer",
  expiresIn: 3600,
};

export const authHandlers = [
  http.get(`${BASE}/api/auth/bootstrap`, () =>
    HttpResponse.json({ userExists: false }),
  ),

  // Framework Identity register: 200 with empty body on success.
  http.post(
    `${BASE}/api/auth/register`,
    () => new HttpResponse(null, { status: 200 }),
  ),

  http.post(`${BASE}/api/auth/login`, () =>
    HttpResponse.json(accessTokenResponse),
  ),

  http.post(`${BASE}/api/auth/refresh`, () =>
    HttpResponse.json(accessTokenResponse),
  ),

  // Current-user resource moved to /api/users/me; no roles field.
  http.get(`${BASE}/api/users/me`, () =>
    HttpResponse.json({
      userId: "user-1",
      email: "admin@example.com",
      locale: "en-US",
      twoFactorEnabled: false,
      credentials: [],
    }),
  ),

  // Framework 2FA management (replaces the custom 2fa/{enable,verify,disable}).
  http.post(`${BASE}/api/auth/manage/2fa`, () =>
    HttpResponse.json({
      sharedKey: "TESTKEY",
      recoveryCodes: null,
      recoveryCodesLeft: 0,
      isTwoFactorEnabled: false,
      isMachineRemembered: false,
    }),
  ),

  // Framework account info (password/email change) replaces /password/change.
  http.post(`${BASE}/api/auth/manage/info`, () =>
    HttpResponse.json({ email: "admin@example.com", isEmailConfirmed: true }),
  ),

  http.put(
    `${BASE}/api/users/me/credentials/streamelements`,
    () => new HttpResponse(null, { status: 204 }),
  ),

  http.delete(
    `${BASE}/api/users/me/credentials/streamelements`,
    () => new HttpResponse(null, { status: 204 }),
  ),

  http.patch(
    `${BASE}/api/users/me/locale`,
    () => new HttpResponse(null, { status: 204 }),
  ),
];
