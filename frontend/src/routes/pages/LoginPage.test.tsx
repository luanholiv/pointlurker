import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import LoginPage from "./LoginPage";
import { ApiError } from "../../lib/apiClient";

vi.mock("../../features/auth/AuthContext", () => ({
  useAuth: vi.fn(),
}));

import { useAuth } from "../../features/auth/AuthContext";
const mockUseAuth = vi.mocked(useAuth);
const mockLogin = vi.fn();

beforeEach(() => {
  vi.clearAllMocks();
  mockUseAuth.mockReturnValue({ login: mockLogin } as never);
});

const renderPage = () =>
  render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>,
  );

const submitCredentials = async () => {
  await userEvent.type(
    screen.getByRole("textbox", { name: /email/i }),
    "a@b.com",
  );
  await userEvent.type(screen.getByLabelText("Password"), "wrong-password");
  await userEvent.click(screen.getByRole("button", { name: /sign in/i }));
};

describe("LoginPage", () => {
  it("renders brand mark", () => {
    renderPage();
    expect(screen.getByText("PointLurker")).toBeInTheDocument();
  });

  it("renders Sign in heading", () => {
    renderPage();
    expect(
      screen.getByRole("heading", { level: 1, name: /sign in/i }),
    ).toBeInTheDocument();
  });

  it("does not render a passkey button, remember-device, or forgot-password controls", () => {
    renderPage();
    expect(
      screen.queryByRole("button", { name: /use passkey/i }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("checkbox", { name: /remember/i }),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByRole("link", { name: /forgot/i }),
    ).not.toBeInTheDocument();
  });

  it("renders bootstrap link", () => {
    renderPage();
    expect(screen.getByText("Bootstrap admin →")).toBeInTheDocument();
  });

  it("renders email and password fields", () => {
    renderPage();
    expect(screen.getByRole("textbox", { name: /email/i })).toBeInTheDocument();
  });

  // The sentence has to come from the catalog: the rejection's own message is a transport detail,
  // in English, on the one page a user sees before any locale preference of theirs is known.
  it("renders the catalog message for a rejected sign-in", async () => {
    mockLogin.mockRejectedValue(
      new ApiError({
        code: "auth.unauthorized",
        status: 401,
        title: "Unauthorized",
      }),
    );
    renderPage();

    await submitCredentials();

    expect(await screen.findByText("Invalid credentials.")).toBeInTheDocument();
  });

  it("falls back to the catalog key when the rejection carries no code", async () => {
    mockLogin.mockRejectedValue(new Error("Failed to fetch"));
    renderPage();

    await submitCredentials();

    expect(await screen.findByText("Sign in failed")).toBeInTheDocument();
  });
});
