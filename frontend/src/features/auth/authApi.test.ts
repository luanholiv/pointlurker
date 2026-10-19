import { describe, it, expect, beforeEach } from "vitest";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import {
  fetchBootstrapState,
  registerUser,
  loginUser,
  fetchCurrentUser,
  enableTwoFactor,
  verifyTwoFactorEnrollment,
  disableTwoFactor,
  setStreamElementsToken,
  clearStreamElementsToken,
  changePassword,
  type AccessTokenResponse,
  type LoginChallengeResponse,
} from "./authApi";

/**
 * A backend error the way `apiClient` recognizes one: only `application/problem+json` is parsed into
 * an `ApiError` carrying `code`, so a plain JSON body of the same shape lands on the generic
 * fallback instead.
 */
const problem = (status: number, body: Record<string, unknown>) =>
  HttpResponse.json(body, {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });

describe("authApi", () => {
  beforeEach(() => {
    localStorage.clear();
  });

  describe("fetchBootstrapState", () => {
    it("returns userExists false by default", async () => {
      const result = await fetchBootstrapState();
      expect(result.userExists).toBe(false);
    });

    it("returns userExists true when handler overridden", async () => {
      server.use(
        http.get("http://localhost/api/auth/bootstrap", () =>
          HttpResponse.json({ userExists: true }),
        ),
      );
      const result = await fetchBootstrapState();
      expect(result.userExists).toBe(true);
    });

    it("throws on server error", async () => {
      server.use(
        http.get(
          "http://localhost/api/auth/bootstrap",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      await expect(fetchBootstrapState()).rejects.toThrow();
    });
  });

  describe("registerUser", () => {
    it("resolves on 200 success (empty body)", async () => {
      await expect(
        registerUser({ email: "a@b.com", password: "P@ss1" }),
      ).resolves.toBeUndefined();
    });

    // Registration now goes through apiClient like every other call: the backend sends the
    // contract, so there is no Identity-specific error type left on this side of the wire.
    it("throws an ApiError carrying the failed rule on a 400", async () => {
      server.use(
        http.post("http://localhost/api/auth/register", () =>
          problem(400, {
            title: "One or more validation errors occurred.",
            code: "validation.failed",
            params: {
              errors: [
                {
                  field: "password",
                  code: "validation.password_too_short",
                  params: { min: 8 },
                },
              ],
            },
          }),
        ),
      );

      await expect(
        registerUser({ email: "a@b.com", password: "x" }),
      ).rejects.toMatchObject({
        name: "ApiError",
        status: 400,
        code: "validation.failed",
      });
    });

    it("surfaces the single-user invariant on a second registration", async () => {
      server.use(
        http.post("http://localhost/api/auth/register", () =>
          problem(400, {
            code: "validation.failed",
            params: {
              errors: [
                { field: "email", code: "users.register_already_exists" },
              ],
            },
          }),
        ),
      );

      await expect(
        registerUser({ email: "a@b.com", password: "P@ss1" }),
      ).rejects.toMatchObject({ code: "validation.failed" });
    });
  });

  describe("loginUser", () => {
    it("returns token on success", async () => {
      const result = await loginUser({ email: "a@b.com", password: "P@ss1" });
      expect("token" in result).toBe(true);
      expect((result as { token: AccessTokenResponse }).token.accessToken).toBe(
        "test-access-token",
      );
    });

    it("returns 2FA challenge on the two-factor code", async () => {
      server.use(
        http.post("http://localhost/api/auth/login", () =>
          problem(401, {
            detail: "RequiresTwoFactor",
            code: "auth.two_factor_required",
          }),
        ),
      );
      const result = await loginUser({ email: "a@b.com", password: "P@ss1" });
      expect("challenge" in result).toBe(true);
      expect(
        (result as { challenge: LoginChallengeResponse }).challenge
          .requiresTwoFactor,
      ).toBe(true);
    });

    // A code is something the page can render its own sentence for; a bare
    // `Error("Login failed with status 401")` is what the login screen would print verbatim.
    it("throws an ApiError carrying the unauthorized code on bad credentials", async () => {
      server.use(
        http.post("http://localhost/api/auth/login", () =>
          problem(401, { code: "auth.unauthorized", title: "Unauthorized" }),
        ),
      );
      await expect(
        loginUser({ email: "a@b.com", password: "wrong" }),
      ).rejects.toMatchObject({
        name: "ApiError",
        status: 401,
        code: "auth.unauthorized",
      });
    });

    it("throws on a 401 that carries no problem body at all", async () => {
      server.use(
        http.post(
          "http://localhost/api/auth/login",
          () => new HttpResponse(null, { status: 401 }),
        ),
      );
      await expect(
        loginUser({ email: "a@b.com", password: "wrong" }),
      ).rejects.toThrow();
    });

    it("throws on 500", async () => {
      server.use(
        http.post(
          "http://localhost/api/auth/login",
          () => new HttpResponse(null, { status: 500 }),
        ),
      );
      await expect(
        loginUser({ email: "a@b.com", password: "P@ss1" }),
      ).rejects.toThrow();
    });
  });

  describe("fetchCurrentUser", () => {
    it("returns user data from /api/users/me without roles", async () => {
      const result = await fetchCurrentUser();
      expect(result.email).toBe("admin@example.com");
      expect(result.locale).toBe("en-US");
      expect("roles" in result).toBe(false);
    });
  });

  describe("enableTwoFactor", () => {
    it("returns sharedKey and a client-built otpauthUri", async () => {
      const result = await enableTwoFactor("admin@example.com");
      expect(result.sharedKey).toBe("TESTKEY");
      expect(result.otpauthUri).toContain("otpauth://");
      expect(result.otpauthUri).toContain("secret=TESTKEY");
    });
  });

  describe("verifyTwoFactorEnrollment", () => {
    it("resolves on success", async () => {
      await expect(verifyTwoFactorEnrollment("123456")).resolves.toBeDefined();
    });

    it("throws on 400 invalid code", async () => {
      server.use(
        http.post(
          "http://localhost/api/auth/manage/2fa",
          () => new HttpResponse(null, { status: 400 }),
        ),
      );
      await expect(verifyTwoFactorEnrollment("000000")).rejects.toThrow();
    });
  });

  describe("disableTwoFactor", () => {
    it("resolves on success", async () => {
      await expect(disableTwoFactor()).resolves.toBeDefined();
    });

    /**
     * `enable: false` alone clears the flag but leaves the TOTP secret live, so the authenticator the
     * owner just revoked keeps producing valid codes and re-enrolling returns the same key.
     */
    it("asks for a shared-key reset so the secret is revoked with the flag", async () => {
      let body: unknown;
      server.use(
        http.post(
          "http://localhost/api/auth/manage/2fa",
          async ({ request }) => {
            body = await request.json();
            return HttpResponse.json({ sharedKey: "ROTATED" });
          },
        ),
      );

      await disableTwoFactor();

      expect(body).toEqual({ enable: false, resetSharedKey: true });
    });
  });

  describe("setStreamElementsToken", () => {
    it("resolves on success", async () => {
      await expect(setStreamElementsToken("se-jwt")).resolves.toBeUndefined();
    });

    it("throws on 400", async () => {
      server.use(
        http.put(
          "http://localhost/api/users/me/credentials/streamelements",
          () => new HttpResponse(null, { status: 400 }),
        ),
      );
      await expect(setStreamElementsToken("")).rejects.toThrow();
    });
  });

  describe("clearStreamElementsToken", () => {
    it("resolves on success", async () => {
      await expect(clearStreamElementsToken()).resolves.toBeUndefined();
    });
  });

  describe("changePassword", () => {
    it("resolves on success", async () => {
      await expect(
        changePassword({ currentPassword: "old", newPassword: "new" }),
      ).resolves.toBeDefined();
    });

    it("throws on 400 validation failure", async () => {
      server.use(
        http.post(
          "http://localhost/api/auth/manage/info",
          () => new HttpResponse(null, { status: 400 }),
        ),
      );
      await expect(
        changePassword({ currentPassword: "wrong", newPassword: "x" }),
      ).rejects.toThrow();
    });
  });
});
