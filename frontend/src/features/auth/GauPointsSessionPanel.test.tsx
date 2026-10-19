import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { vi } from "vitest";
import { ApiError } from "../../lib/apiClient";
import GauPointsSessionPanel from "./GauPointsSessionPanel";
import type { CredentialSummary } from "./authApi";

vi.mock("./AuthContext", () => ({
  useAuth: vi.fn(),
}));

import { useAuth } from "./AuthContext";
const mockUseAuth = vi.mocked(useAuth);

const setProviderCredential = vi.fn();
const clearProviderCredential = vi.fn();

const withCredentials = (credentials: CredentialSummary[]) =>
  mockUseAuth.mockReturnValue({
    user: {
      userId: "1",
      email: "a@b.com",
      twoFactorEnabled: false,
      locale: "en-US",
      credentials,
    },
    setProviderCredential,
    clearProviderCredential,
  } as unknown as ReturnType<typeof useAuth>);

const session = (overrides: Partial<CredentialSummary> = {}) => ({
  provider: "gaupoints" as const,
  accountName: "someviewer",
  expiresAt: "2999-01-01T12:00:00Z",
  lastValidatedAt: "2998-12-31T12:00:00Z",
  lastValidationError: null,
  ...overrides,
});

describe("GauPointsSessionPanel", () => {
  beforeEach(() => {
    setProviderCredential.mockReset();
    clearProviderCredential.mockReset();
  });

  it("says no session is stored and offers no clear button", () => {
    withCredentials([]);
    render(<GauPointsSessionPanel />);

    expect(screen.getByText("Not set")).toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: "Clear session" }),
    ).not.toBeInTheDocument();
  });

  it("shows the account and when the stored session expires", () => {
    withCredentials([session()]);
    render(<GauPointsSessionPanel />);

    expect(screen.getByText("Configured")).toBeInTheDocument();
    expect(screen.getByText("Signed in as someviewer")).toBeInTheDocument();
    expect(screen.getByText(/^Expires /)).toBeInTheDocument();
  });

  it("marks a session past its expiry as expired", () => {
    withCredentials([session({ expiresAt: "2000-01-01T00:00:00Z" })]);
    render(<GauPointsSessionPanel />);

    expect(screen.getByText("Expired")).toBeInTheDocument();
    expect(screen.getByText(/^Expired /)).toBeInTheDocument();
  });

  it("says GauPoints refused the session, in the catalog's words", () => {
    withCredentials([session({ lastValidationError: "credentials.rejected" })]);
    render(<GauPointsSessionPanel />);

    expect(screen.getByText("Refused")).toBeInTheDocument();
    expect(
      screen.getByText(
        "The provider did not accept this credential. Check it and try again.",
      ),
    ).toBeInTheDocument();
  });

  it("saves the pasted value as the GauPoints credential", async () => {
    setProviderCredential.mockResolvedValue(undefined);
    withCredentials([]);
    render(<GauPointsSessionPanel />);

    await userEvent.type(
      screen.getByLabelText("kp_session cookie"),
      "jwt-value",
    );
    await userEvent.click(screen.getByRole("button", { name: "Save session" }));

    expect(setProviderCredential).toHaveBeenCalledWith(
      "gaupoints",
      "jwt-value",
    );
    expect(await screen.findByText("Session saved.")).toBeInTheDocument();
  });

  it("shows why a save was refused", async () => {
    setProviderCredential.mockRejectedValue(
      new ApiError({
        code: "credentials.expired",
        params: { provider: "gaupoints" },
        status: 422,
        title: "Expired",
      }),
    );
    withCredentials([]);
    render(<GauPointsSessionPanel />);

    await userEvent.type(screen.getByLabelText("kp_session cookie"), "old");
    await userEvent.click(screen.getByRole("button", { name: "Save session" }));

    expect(
      await screen.findByText(
        "The credential has already expired. Copy a fresh one and try again.",
      ),
    ).toBeInTheDocument();
  });

  it("clears the stored session", async () => {
    clearProviderCredential.mockResolvedValue(undefined);
    withCredentials([session()]);
    render(<GauPointsSessionPanel />);

    await userEvent.click(
      screen.getByRole("button", { name: "Clear session" }),
    );

    expect(clearProviderCredential).toHaveBeenCalledWith("gaupoints");
  });
});
