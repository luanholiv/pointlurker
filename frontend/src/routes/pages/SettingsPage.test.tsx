import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { MemoryRouter, useLocation } from "react-router";
import SettingsPage from "./SettingsPage";
import { ApiError } from "../../lib/apiClient";

const LocationProbe = () => {
  const location = useLocation();
  return <div data-testid="location-hash">{location.hash}</div>;
};

const renderAt = (entry = "/settings") =>
  render(
    <MemoryRouter initialEntries={[entry]}>
      <SettingsPage />
      <LocationProbe />
    </MemoryRouter>,
  );

const mockSetStreamElementsToken = vi.fn();
const mockClearStreamElementsToken = vi.fn();

vi.mock("../../features/auth/AuthContext", () => ({
  useAuth: vi.fn(),
}));

import { useAuth } from "../../features/auth/AuthContext";
const mockUseAuth = vi.mocked(useAuth);

const baseAuth = {
  bootstrapState: { userExists: true },
  bootstrapStatus: "ready" as const,
  isAuthenticated: true,
  isUserLoading: false,
  login: vi.fn(),
  register: vi.fn(),
  enableTwoFactor: vi.fn(),
  verifyTwoFactorEnrollment: vi.fn(),
  disableTwoFactor: vi.fn(),
  changePassword: vi.fn(),
  setStreamElementsToken: mockSetStreamElementsToken,
  clearStreamElementsToken: mockClearStreamElementsToken,
  setProviderCredential: vi.fn(),
  clearProviderCredential: vi.fn(),
  changeLocale: vi.fn(),
  logout: vi.fn(),
};

const goToTokenTab = async () => {
  await userEvent.click(screen.getByRole("tab", { name: "Store credentials" }));
};

describe("SettingsPage - account credentials", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("lists every provider's credential status on the account tab", () => {
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "1",
        email: "a@b.com",
        credentials: [
          {
            provider: "gaupoints",
            accountName: null,
            expiresAt: null,
            lastValidatedAt: null,
            lastValidationError: null,
          },
        ],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    renderAt();
    expect(screen.getByText("GauPoints").parentElement).toHaveTextContent(
      "Configured",
    );
    expect(screen.getByText("StreamElements").parentElement).toHaveTextContent(
      "Not set",
    );
  });
});

describe("SettingsPage - StreamElements Token", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows "Not set" when no SE token is configured', async () => {
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "1",
        email: "a@b.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    renderAt();
    await goToTokenTab();
    // The GauPoints panel below has a "Not set" pill of its own.
    expect(screen.getAllByText("Not set")).toHaveLength(2);
    expect(screen.queryByText("Clear Token")).not.toBeInTheDocument();
  });

  it("says where to find the JWT token", async () => {
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "1",
        email: "a@b.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    renderAt();
    await goToTokenTab();
    expect(
      screen.getByText(/streamelements\.com\/dashboard\/account\/channels/),
    ).toBeInTheDocument();
  });

  it('shows "Configured" and Clear button when SE token exists', async () => {
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "1",
        email: "a@b.com",
        credentials: [
          {
            provider: "streamelements" as const,
            accountName: null,
            expiresAt: null,
            lastValidatedAt: null,
            lastValidationError: null,
          },
        ],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    renderAt();
    await goToTokenTab();
    expect(screen.getByText("Configured")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Clear Token" }),
    ).toBeInTheDocument();
  });

  it("calls setStreamElementsToken on form submit", async () => {
    mockSetStreamElementsToken.mockResolvedValue(undefined);
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "1",
        email: "a@b.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    renderAt();
    await goToTokenTab();

    const input = screen.getByPlaceholderText("Paste your JWT token here...");
    await userEvent.type(input, "eyJhbGciOiJIUzI1NiJ9.test");
    await userEvent.click(screen.getByRole("button", { name: "Save Token" }));

    expect(mockSetStreamElementsToken).toHaveBeenCalledWith(
      "eyJhbGciOiJIUzI1NiJ9.test",
    );
  });

  it("calls clearStreamElementsToken on clear click", async () => {
    mockClearStreamElementsToken.mockResolvedValue(undefined);
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "1",
        email: "a@b.com",
        credentials: [
          {
            provider: "streamelements" as const,
            accountName: null,
            expiresAt: null,
            lastValidatedAt: null,
            lastValidationError: null,
          },
        ],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    renderAt();
    await goToTokenTab();

    await userEvent.click(screen.getByRole("button", { name: "Clear Token" }));

    expect(mockClearStreamElementsToken).toHaveBeenCalled();
  });

  it("shows error message when save fails", async () => {
    mockSetStreamElementsToken.mockRejectedValue(new Error("Network error"));
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "1",
        email: "a@b.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
    renderAt();
    await goToTokenTab();

    const input = screen.getByPlaceholderText("Paste your JWT token here...");
    await userEvent.type(input, "test-token");
    await userEvent.click(screen.getByRole("button", { name: "Save Token" }));

    // The rejection carries a developer message; the user reads the catalog fallback.
    expect(await screen.findByText("Failed to save token")).toBeInTheDocument();
  });
});

/**
 * The message a user reads must come from the catalog, never off the rejection. `ApiError.message`
 * is `title ?? code`, and the backend title is English developer copy, so rendering it puts English
 * on a pt-BR screen. A contract error resolves through its `code`; anything else resolves the
 * handler's fallback key.
 */
describe("SettingsPage - error copy comes from the catalog", () => {
  const authWith = (overrides: Partial<typeof baseAuth> = {}) => ({
    ...baseAuth,
    ...overrides,
    user: {
      userId: "1",
      email: "a@b.com",
      credentials: [
        {
          provider: "streamelements" as const,
          accountName: null,
          expiresAt: null,
          lastValidatedAt: null,
          lastValidationError: null,
        },
      ],
      twoFactorEnabled: false,
      locale: "en-US",
    },
  });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the catalog message for the code when saving the token fails", async () => {
    mockSetStreamElementsToken.mockRejectedValue(
      new ApiError({
        code: "credentials.rejected",
        params: { provider: "streamelements" },
        status: 422,
        title: "The provider did not accept this credential.",
      }),
    );
    mockUseAuth.mockReturnValue(authWith());
    renderAt("/settings#token");

    const input = screen.getByPlaceholderText("Paste your JWT token here...");
    await userEvent.type(input, "test-token");
    await userEvent.click(screen.getByRole("button", { name: "Save Token" }));

    expect(
      await screen.findByText(
        "The provider did not accept this credential. Check it and try again.",
      ),
    ).toBeInTheDocument();
  });

  it("renders the catalog message for the code when clearing the token fails", async () => {
    mockClearStreamElementsToken.mockRejectedValue(
      new ApiError({
        code: "auth.provider_credential_not_configured",
        status: 400,
        title: "No StreamElements credential is configured",
      }),
    );
    mockUseAuth.mockReturnValue(authWith());
    renderAt("/settings#token");

    await userEvent.click(screen.getByRole("button", { name: "Clear Token" }));

    expect(
      await screen.findByText(
        "Add this provider's credential in Settings before adding its store.",
      ),
    ).toBeInTheDocument();
  });

  it("falls back to the catalog key when clearing the token rejects without a code", async () => {
    mockClearStreamElementsToken.mockRejectedValue(new Error("Network error"));
    mockUseAuth.mockReturnValue(authWith());
    renderAt("/settings#token");

    await userEvent.click(screen.getByRole("button", { name: "Clear Token" }));

    expect(
      await screen.findByText("Failed to clear token"),
    ).toBeInTheDocument();
  });

  /**
   * The password change posts through `apiClient`, so Identity's validation failure arrives as a
   * `validation.failed` ApiError. Resolving only that outer code would render "An unexpected error
   * occurred." — worse than the English rule it replaced. The rule is in `params.errors[]`.
   */
  it("names the password rule that failed rather than a generic error", async () => {
    const changePassword = vi.fn().mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "password",
              code: "validation.password_requires_digit",
            },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    mockUseAuth.mockReturnValue(authWith({ changePassword }));
    renderAt("/settings#security");

    await userEvent.type(screen.getByLabelText("Current password"), "oldpass1");
    await userEvent.type(screen.getByLabelText("New password"), "newpass");
    await userEvent.type(
      screen.getByLabelText("Confirm new password"),
      "newpass",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Update password" }),
    );

    expect(
      await screen.findByText("Password must contain at least one digit."),
    ).toBeInTheDocument();
  });

  it("shows every password rule that failed, attached to the new-password input", async () => {
    const changePassword = vi.fn().mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            { field: "password", code: "validation.password_requires_digit" },
            { field: "password", code: "validation.password_requires_upper" },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    mockUseAuth.mockReturnValue(authWith({ changePassword }));
    renderAt("/settings#security");

    await userEvent.type(screen.getByLabelText("Current password"), "oldpass1");
    await userEvent.type(screen.getByLabelText("New password"), "newpass");
    await userEvent.type(
      screen.getByLabelText("Confirm new password"),
      "newpass",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Update password" }),
    );

    await waitFor(() =>
      expect(screen.getByLabelText("New password")).toHaveAccessibleDescription(
        "Password must contain at least one digit. Password must contain at least one uppercase letter.",
      ),
    );
    expect(screen.getByLabelText("New password")).toHaveAttribute(
      "aria-invalid",
      "true",
    );
  });

  it("attaches a rejected StreamElements token to its own input", async () => {
    const setStreamElementsToken = vi.fn().mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [{ field: "secret", code: "credentials.secret_required" }],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    mockUseAuth.mockReturnValue(authWith({ setStreamElementsToken }));
    renderAt("/settings#token");

    await userEvent.type(screen.getByLabelText("JWT Token"), "x");
    await userEvent.click(screen.getByRole("button", { name: "Save Token" }));

    await waitFor(() =>
      expect(screen.getByLabelText("JWT Token")).toHaveAccessibleDescription(
        "A credential is required.",
      ),
    );
  });

  it("interpolates the code params when a locale change fails", async () => {
    const changeLocale = vi.fn().mockRejectedValue(
      new ApiError({
        code: "users.locale_unsupported",
        params: { locale: "pt-BR" },
        status: 400,
        title: "Locale is not supported",
      }),
    );
    mockUseAuth.mockReturnValue(authWith({ changeLocale }));
    renderAt();

    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: "Change language" }),
      "pt-BR",
    );

    expect(
      await screen.findByText("Locale 'pt-BR' is not supported."),
    ).toBeInTheDocument();
  });

  it("falls back to the catalog key when a locale change rejects without a code", async () => {
    const changeLocale = vi.fn().mockRejectedValue(new Error("boom"));
    mockUseAuth.mockReturnValue(authWith({ changeLocale }));
    renderAt();

    await userEvent.selectOptions(
      screen.getByRole("combobox", { name: "Change language" }),
      "pt-BR",
    );

    expect(
      await screen.findByText("Failed to update language"),
    ).toBeInTheDocument();
  });
});

/**
 * These three handlers had no `catch` at all, so a rejection was an unhandled promise rejection: a
 * stack trace in the console and an inert-looking button on screen. Neither lint selector could see
 * it — both need a `catch` to exist — so these are the guard.
 */
describe("SettingsPage - the 2FA handlers report their failures", () => {
  const setup = { sharedKey: "ABCDEFGHIJKLMNOP", otpauthUri: "otpauth://x" };

  const authWith2FA = (
    overrides: Partial<typeof baseAuth> = {},
    twoFactorEnabled = false,
  ) => ({
    ...baseAuth,
    ...overrides,
    user: {
      userId: "1",
      email: "a@b.com",
      credentials: [
        {
          provider: "streamelements" as const,
          accountName: null,
          expiresAt: null,
          lastValidatedAt: null,
          lastValidationError: null,
        },
      ],
      twoFactorEnabled,
      locale: "en-US",
    },
  });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("renders the catalog message when starting the setup fails", async () => {
    const enableTwoFactor = vi.fn().mockRejectedValue(
      new ApiError({
        code: "auth.unauthorized",
        status: 401,
        title: "Unauthorized",
      }),
    );
    mockUseAuth.mockReturnValue(authWith2FA({ enableTwoFactor }));
    renderAt("/settings#security");

    await userEvent.click(
      screen.getByRole("button", { name: "Enable two-factor" }),
    );

    expect(await screen.findByText("Invalid credentials.")).toBeInTheDocument();
  });

  it("falls back to the catalog key when starting the setup rejects without a code", async () => {
    const enableTwoFactor = vi.fn().mockRejectedValue(new Error("Network"));
    mockUseAuth.mockReturnValue(authWith2FA({ enableTwoFactor }));
    renderAt("/settings#security");

    await userEvent.click(
      screen.getByRole("button", { name: "Enable two-factor" }),
    );

    expect(
      await screen.findByText("Failed to start two-factor setup"),
    ).toBeInTheDocument();
  });

  /**
   * The shape the real backend now sends for a refused enrolment code: Identity answers
   * `manage/2fa` with a `ValidationProblem`, so the code that names the failure is the nested one,
   * anchored to the input it was typed into.
   */
  it("names the refused code against the verification input", async () => {
    const enableTwoFactor = vi.fn().mockResolvedValue(setup);
    const verifyTwoFactorEnrollment = vi.fn().mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "twoFactorCode",
              code: "auth.two_factor_invalid",
              params: {},
            },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    mockUseAuth.mockReturnValue(
      authWith2FA({ enableTwoFactor, verifyTwoFactorEnrollment }),
    );
    renderAt("/settings#security");

    await userEvent.click(
      screen.getByRole("button", { name: "Enable two-factor" }),
    );
    await userEvent.type(
      await screen.findByLabelText("Verification code"),
      "123456",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Verify and enable" }),
    );

    const message = await screen.findByText(
      "Invalid two-factor authentication code.",
    );
    expect(message).toBeInTheDocument();

    // Attached to the input, not just somewhere on the panel.
    const input = screen.getByLabelText("Verification code");
    expect(input).toHaveAttribute("aria-invalid", "true");
    expect(message.closest("div")).toHaveAttribute(
      "id",
      input.getAttribute("aria-describedby"),
    );
  });

  it("falls back to the catalog key when the verification rejects without a code", async () => {
    const enableTwoFactor = vi.fn().mockResolvedValue(setup);
    const verifyTwoFactorEnrollment = vi
      .fn()
      .mockRejectedValue(new Error("Network"));
    mockUseAuth.mockReturnValue(
      authWith2FA({ enableTwoFactor, verifyTwoFactorEnrollment }),
    );
    renderAt("/settings#security");

    await userEvent.click(
      screen.getByRole("button", { name: "Enable two-factor" }),
    );
    await userEvent.type(
      await screen.findByLabelText("Verification code"),
      "123456",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Verify and enable" }),
    );

    expect(
      await screen.findByText("Failed to verify the code"),
    ).toBeInTheDocument();
  });

  it("renders a message when disabling fails", async () => {
    const disableTwoFactor = vi.fn().mockRejectedValue(new Error("Network"));
    mockUseAuth.mockReturnValue(authWith2FA({ disableTwoFactor }, true));
    renderAt("/settings#security");

    await userEvent.click(screen.getByRole("button", { name: "Disable" }));
    await userEvent.click(screen.getByRole("button", { name: "Turn it off" }));

    expect(
      await screen.findByText("Failed to disable two-factor authentication"),
    ).toBeInTheDocument();
  });

  it("clears a previous failure when the action is retried", async () => {
    const enableTwoFactor = vi
      .fn()
      .mockRejectedValueOnce(new Error("Network"))
      .mockResolvedValueOnce(setup);
    mockUseAuth.mockReturnValue(authWith2FA({ enableTwoFactor }));
    renderAt("/settings#security");

    const button = screen.getByRole("button", { name: "Enable two-factor" });
    await userEvent.click(button);
    expect(
      await screen.findByText("Failed to start two-factor setup"),
    ).toBeInTheDocument();

    await userEvent.click(button);

    await waitFor(() => {
      expect(
        screen.queryByText("Failed to start two-factor setup"),
      ).not.toBeInTheDocument();
    });
  });
});

describe("SettingsPage - Tabs", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      user: {
        userId: "u1",
        email: "admin@test.com",
        credentials: [],
        twoFactorEnabled: false,
        locale: "en-US",
      },
    });
  });

  it("Account tab is active by default", () => {
    renderAt();
    expect(screen.getByRole("tab", { name: "Account" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
  });

  it("tab switch shows Security content and sets #security hash", async () => {
    renderAt();
    await userEvent.click(screen.getByRole("tab", { name: "Security" }));
    expect(
      screen.getByRole("button", { name: "Update password" }),
    ).toBeInTheDocument();
    expect(screen.getByTestId("location-hash")).toHaveTextContent("#security");
  });

  it("deep link #security selects the Security tab", () => {
    renderAt("/settings#security");
    expect(screen.getByRole("tab", { name: "Security" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
    expect(
      screen.getByRole("button", { name: "Update password" }),
    ).toBeInTheDocument();
  });

  it("deep link #token selects the store credentials tab", () => {
    renderAt("/settings#token");
    expect(
      screen.getByRole("tab", { name: "Store credentials" }),
    ).toHaveAttribute("aria-selected", "true");
    expect(
      screen.getByPlaceholderText("Paste your JWT token here..."),
    ).toBeInTheDocument();
  });

  it("unknown hash falls back to the Account tab", () => {
    renderAt("/settings#bogus");
    expect(screen.getByRole("tab", { name: "Account" })).toHaveAttribute(
      "aria-selected",
      "true",
    );
  });

  it("Revoke all sessions button has danger style", async () => {
    renderAt();
    await userEvent.click(screen.getByRole("tab", { name: "Security" }));
    const btn = screen.getByRole("button", { name: "Revoke all sessions" });
    expect(btn).toHaveClass("btn-danger");
  });
});

/**
 * The panel reads the enabled flag off the profile. Holding it in component state seeded to false
 * would offer enrolment again on every load to an account that already has an authenticator, and
 * leave the Disable button unreachable outside the session that enrolled.
 */
describe("SettingsPage - the 2FA panel reflects the stored account state", () => {
  const userWith = (twoFactorEnabled: boolean) => ({
    userId: "1",
    email: "a@b.com",
    credentials: [
      {
        provider: "streamelements" as const,
        accountName: null,
        expiresAt: null,
        lastValidatedAt: null,
        lastValidationError: null,
      },
    ],
    twoFactorEnabled,
    locale: "en-US",
  });

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("shows the enabled state on first paint when the account already has 2FA", () => {
    mockUseAuth.mockReturnValue({ ...baseAuth, user: userWith(true) });
    renderAt("/settings#security");

    expect(screen.getByText("Two-factor is enabled.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Disable" })).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Enable two-factor" }),
    ).not.toBeInTheDocument();
  });

  it("offers enrolment when the account has no 2FA", () => {
    mockUseAuth.mockReturnValue({ ...baseAuth, user: userWith(false) });
    renderAt("/settings#security");

    expect(
      screen.getByRole("button", { name: "Enable two-factor" }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Disable" }),
    ).not.toBeInTheDocument();
  });

  it("leaves 2FA on when the confirmation is dismissed", async () => {
    const disableTwoFactor = vi.fn().mockResolvedValue(undefined);
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      disableTwoFactor,
      user: userWith(true),
    });
    renderAt("/settings#security");

    await userEvent.click(screen.getByRole("button", { name: "Disable" }));
    expect(
      await screen.findByText("Turn off two-factor authentication?"),
    ).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Cancel" }));

    expect(disableTwoFactor).not.toHaveBeenCalled();
  });

  it("disables only once the confirmation is accepted", async () => {
    const disableTwoFactor = vi.fn().mockResolvedValue(undefined);
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      disableTwoFactor,
      user: userWith(true),
    });
    renderAt("/settings#security");

    await userEvent.click(screen.getByRole("button", { name: "Disable" }));
    await userEvent.click(screen.getByRole("button", { name: "Turn it off" }));

    expect(disableTwoFactor).toHaveBeenCalledTimes(1);
  });

  it("renders a scannable QR for the otpauth URI alongside the key", async () => {
    const enableTwoFactor = vi.fn().mockResolvedValue({
      sharedKey: "ABCDEFGHIJKLMNOP",
      otpauthUri: "otpauth://totp/PointLurker:a@b.com?secret=ABCDEFGHIJKLMNOP",
    });
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      enableTwoFactor,
      user: userWith(false),
    });
    renderAt("/settings#security");

    await userEvent.click(
      screen.getByRole("button", { name: "Enable two-factor" }),
    );

    // The QR carries the secret as a picture; the key stays on screen for manual entry.
    expect(
      await screen.findByRole("img", {
        name: "Scan this key in your authenticator app.",
      }),
    ).toBeInTheDocument();
    expect(screen.getByText("ABCDEFGHIJKLMNOP")).toBeInTheDocument();
  });

  /**
   * Identity issues these once and never again, so a lost set is unrecoverable — the panel has to
   * put them on screen the moment enrolment returns them.
   */
  it("shows the recovery codes issued by a successful enrolment", async () => {
    const enableTwoFactor = vi
      .fn()
      .mockResolvedValue({ sharedKey: "KEY", otpauthUri: "otpauth://x" });
    const verifyTwoFactorEnrollment = vi
      .fn()
      .mockResolvedValue({ recoveryCodes: ["aaaa-1111", "bbbb-2222"] });
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      enableTwoFactor,
      verifyTwoFactorEnrollment,
      user: userWith(false),
    });
    renderAt("/settings#security");

    await userEvent.click(
      screen.getByRole("button", { name: "Enable two-factor" }),
    );
    await userEvent.type(
      await screen.findByLabelText("Verification code"),
      "123456",
    );
    await userEvent.click(
      screen.getByRole("button", { name: "Verify and enable" }),
    );

    const codes = await screen.findByRole("list", { name: "Recovery codes" });
    expect(codes).toHaveTextContent("aaaa-1111");
    expect(codes).toHaveTextContent("bbbb-2222");

    // Acknowledging clears them from the page — they are not meant to linger on a settings screen.
    await userEvent.click(screen.getByRole("button", { name: "I saved them" }));

    expect(
      screen.queryByRole("list", { name: "Recovery codes" }),
    ).not.toBeInTheDocument();
  });

  /**
   * Escape reaches the dialog's own close path rather than the footer Cancel button, and a stray
   * keypress must not be a way to switch two-factor off.
   */
  it("closes the disable confirmation on Escape without disabling", async () => {
    const disableTwoFactor = vi.fn().mockResolvedValue(undefined);
    mockUseAuth.mockReturnValue({
      ...baseAuth,
      disableTwoFactor,
      user: userWith(true),
    });
    renderAt("/settings#security");

    await userEvent.click(screen.getByRole("button", { name: "Disable" }));
    expect(
      await screen.findByText("Turn off two-factor authentication?"),
    ).toBeInTheDocument();

    await userEvent.keyboard("{Escape}");

    await waitFor(() => {
      expect(
        screen.queryByText("Turn off two-factor authentication?"),
      ).not.toBeInTheDocument();
    });
    expect(disableTwoFactor).not.toHaveBeenCalled();
  });
});
