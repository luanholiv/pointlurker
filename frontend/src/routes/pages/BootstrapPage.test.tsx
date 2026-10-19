import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import BootstrapPage from "./BootstrapPage";
import { ApiError } from "../../lib/apiClient";

vi.mock("../../features/auth/AuthContext", () => ({
  useAuth: vi.fn(),
}));

import { useAuth } from "../../features/auth/AuthContext";
const mockUseAuth = vi.mocked(useAuth);
const mockRegister = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  mockUseAuth.mockReturnValue({ register: mockRegister } as never);
});

const renderPage = () =>
  render(
    <MemoryRouter>
      <BootstrapPage />
    </MemoryRouter>,
  );

const submitNewAccount = async (password = "short") => {
  await userEvent.type(
    screen.getByPlaceholderText("you@domain.tld"),
    "a@b.com",
  );
  await userEvent.type(
    screen.getByPlaceholderText("At least 12 chars"),
    password,
  );
  await userEvent.type(screen.getByLabelText("Confirm new password"), password);
  await userEvent.click(screen.getByRole("button", { name: "Create admin" }));
};

describe("BootstrapPage", () => {
  it("renders eyebrow text", () => {
    renderPage();
    expect(screen.getByText(/First run · one-time/i)).toBeInTheDocument();
  });

  it("does not render a display-name field or a passkey toggle", () => {
    renderPage();
    expect(screen.queryByPlaceholderText("admin")).not.toBeInTheDocument();
    expect(
      screen.queryByRole("button", { name: /Register a passkey/i }),
    ).not.toBeInTheDocument();
  });

  it("renders sign-in back link", () => {
    renderPage();
    expect(screen.getByText("← Sign in")).toBeInTheDocument();
  });

  it("renders email, password and confirm fields", () => {
    renderPage();
    expect(screen.getByPlaceholderText("you@domain.tld")).toBeInTheDocument();
    expect(
      screen.getByPlaceholderText("At least 12 chars"),
    ).toBeInTheDocument();
  });

  /**
   * This page had the worst of the ten: it printed the rejection's message, which for an Identity
   * validation failure is the summary title — "One or more validation errors occurred." The rule
   * that actually failed was in the payload the whole time.
   */
  it("names the password rule that failed instead of the summary", async () => {
    mockRegister.mockRejectedValue(
      new ApiError({
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
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    renderPage();

    await submitNewAccount();

    expect(
      await screen.findByText("Password must be at least 8 characters."),
    ).toBeInTheDocument();
  });

  // Identity reports one entry per broken password rule, so this is the common multi-entry shape.
  it("shows every password rule that failed, not just the first", async () => {
    mockRegister.mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "password",
              code: "validation.password_requires_digit",
            },
            {
              field: "password",
              code: "validation.password_requires_upper",
            },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    renderPage();

    await submitNewAccount();

    expect(
      await screen.findByText("Password must contain at least one digit."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Password must contain at least one uppercase letter."),
    ).toBeInTheDocument();
    // Both belong to the same input, so both are in its description rather than one replacing it.
    expect(
      screen.getByPlaceholderText("At least 12 chars"),
    ).toHaveAccessibleDescription(
      "Password must contain at least one digit. Password must contain at least one uppercase letter.",
    );
  });

  it("attaches a password rule to the password input", async () => {
    mockRegister.mockRejectedValue(
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
    renderPage();

    await submitNewAccount();

    const input = screen.getByPlaceholderText("At least 12 chars");
    await waitFor(() =>
      expect(input).toHaveAccessibleDescription(
        "Password must contain at least one digit.",
      ),
    );
    expect(input).toHaveAttribute("aria-invalid", "true");
  });

  it("attaches an email rule to the email input", async () => {
    mockRegister.mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            {
              field: "email",
              code: "validation.invalid_email",
              params: { email: "nope" },
            },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    renderPage();

    await submitNewAccount();

    await waitFor(() =>
      expect(
        screen.getByPlaceholderText("you@domain.tld"),
      ).toHaveAccessibleDescription("Email 'nope' is invalid."),
    );
  });

  it("reports the single-user invariant on a second registration", async () => {
    mockRegister.mockRejectedValue(
      new ApiError({
        code: "validation.failed",
        params: {
          errors: [{ field: "email", code: "users.register_already_exists" }],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
    );
    renderPage();

    await submitNewAccount("LongEnoughPassword1!");

    expect(
      await screen.findByText("An account already exists."),
    ).toBeInTheDocument();
  });

  it("falls back to the catalog key when the rejection carries no code", async () => {
    mockRegister.mockRejectedValue(new Error("Failed to fetch"));
    renderPage();

    await submitNewAccount("LongEnoughPassword1!");

    expect(
      await screen.findByText("Unable to create admin account"),
    ).toBeInTheDocument();
  });
});
