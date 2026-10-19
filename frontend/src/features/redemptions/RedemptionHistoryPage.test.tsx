import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";

vi.mock("./useRedemptions", () => ({
  useRedemptions: vi.fn(),
}));

import RedemptionHistoryPage from "./RedemptionHistoryPage";
import { useRedemptions } from "./useRedemptions";
import type { RedemptionItem } from "./redemptionsApi";
const mockUseRedemptions = vi.mocked(useRedemptions);

const makeResponse = (items: RedemptionItem[]) => ({
  items,
  totalCount: items.length,
  offset: 0,
  limit: 20,
});

const successItem = {
  id: "r1",
  status: "Completed" as const,
  storeItemName: "Cool Skin",
  storeName: "Store Alpha",
  attemptedAt: "2026-04-01T10:00:00Z",
  submittedAt: "2026-04-01T10:00:01Z",
  settledAt: "2026-04-01T10:00:02Z",
  failureCode: null,
  failureContext: null,
  pollFailures: 0,
  provider: "streamelements" as const,
};

const failedItem = {
  id: "r2",
  status: "SubmissionFailed" as const,
  storeItemName: "Rare Item",
  storeName: "Store Beta",
  attemptedAt: "2026-04-02T10:00:00Z",
  submittedAt: null,
  settledAt: null,
  failureCode: "redemption.failure.provider_api_unauthorized",
  failureContext: "HTTP 401: Unauthorized",
  pollFailures: 0,
  provider: "streamelements" as const,
};

describe("RedemptionHistoryPage", () => {
  it("Search button toggles filter panel", async () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(
      screen.queryByRole("button", { name: "All" }),
    ).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(screen.getByRole("button", { name: "All" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(
      screen.queryByRole("button", { name: "All" }),
    ).not.toBeInTheDocument();
  });

  it("All chip is active by default (pressed)", async () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    const allChip = screen.getByRole("button", { name: "All" });
    expect(allChip).toHaveAttribute("aria-pressed", "true");
  });

  it("shows empty state when no redemptions", () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(
      screen.getByRole("heading", { name: "No redemptions yet" }),
    ).toBeInTheDocument();
  });

  it("a filter matching nothing offers to clear it instead of the first-run message", async () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    await userEvent.click(screen.getByRole("button", { name: "Failed" }));

    expect(
      screen.getByRole("heading", { name: "No redemptions match" }),
    ).toBeInTheDocument();

    await userEvent.click(
      screen.getByRole("button", { name: "Clear filters" }),
    );

    expect(screen.getByRole("button", { name: "All" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    expect(
      screen.getByRole("heading", { name: "No redemptions yet" }),
    ).toBeInTheDocument();
  });

  it("renders redemption rows", () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([successItem, failedItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(screen.getByText("Cool Skin")).toBeInTheDocument();
    expect(screen.getByText("Rare Item")).toBeInTheDocument();
  });

  it("Failed chip sets pressed state and passes filter to hook", async () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([failedItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    const failedChip = screen.getByRole("button", { name: "Failed" });
    await userEvent.click(failedChip);
    expect(failedChip).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: "All" })).toHaveAttribute(
      "aria-pressed",
      "false",
    );
    expect(mockUseRedemptions).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: "SubmissionFailed" }),
    );
  });

  /**
   * Submitted is the state a redemption gets stuck in when its completion cannot be read. Without
   * a chip of its own those rows could not be listed at all.
   */
  it("Submitted chip passes the submitted filter to the hook", async () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    const submittedChip = screen.getByRole("button", { name: "Submitted" });
    await userEvent.click(submittedChip);
    expect(submittedChip).toHaveAttribute("aria-pressed", "true");
    expect(mockUseRedemptions).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: "Submitted" }),
    );
  });

  /**
   * A submitted row whose status check keeps failing has no failure code, so without a signal of
   * its own it reads as an ordinary pending row and a broken completion check stays invisible.
   */
  it("reports a submitted row whose status check keeps failing", () => {
    const wedgedItem = {
      id: "r4",
      status: "Submitted" as const,
      storeItemName: "Stuck Item",
      storeName: "Store Delta",
      attemptedAt: "2026-04-04T10:00:00Z",
      submittedAt: "2026-04-04T10:00:01Z",
      settledAt: null,
      failureCode: null,
      failureContext: null,
      pollFailures: 7,
      provider: "streamelements" as const,
    };
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([wedgedItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(
      screen.getByText("Status check failing (7 attempts)"),
    ).toBeInTheDocument();
  });

  it("leaves the reason blank for a submitted row that is simply waiting", () => {
    const waitingItem = {
      id: "r5",
      status: "Submitted" as const,
      storeItemName: "Waiting Item",
      storeName: "Store Epsilon",
      attemptedAt: "2026-04-05T10:00:00Z",
      submittedAt: "2026-04-05T10:00:01Z",
      settledAt: null,
      failureCode: null,
      failureContext: null,
      pollFailures: 0,
      provider: "streamelements" as const,
    };
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([waitingItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(screen.getByText("Waiting Item")).toBeInTheDocument();
    expect(screen.queryByText(/Status check failing/)).not.toBeInTheDocument();
  });

  it("renders a rejected redemption with its own label", () => {
    const rejectedItem = {
      id: "r6",
      status: "Rejected" as const,
      storeItemName: "Refused Item",
      storeName: "Store Zeta",
      attemptedAt: "2026-04-06T10:00:00Z",
      submittedAt: "2026-04-06T10:00:01Z",
      settledAt: "2026-04-06T10:30:00Z",
      failureCode: null,
      failureContext: null,
      pollFailures: 0,
      provider: "streamelements" as const,
    };
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([rejectedItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(screen.getByText("Rejected")).toBeInTheDocument();
  });

  it("renders the insufficient-points failure message from its error code", () => {
    const insufficientItem = {
      id: "r3",
      status: "SubmissionFailed" as const,
      storeItemName: "Pricey Item",
      storeName: "Store Gamma",
      attemptedAt: "2026-04-03T10:00:00Z",
      submittedAt: null,
      settledAt: null,
      failureCode: "redemption.failure.insufficient_points",
      failureContext: "HTTP 400: Sorry you don't have enough points",
      pollFailures: 0,
      provider: "streamelements" as const,
    };
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([insufficientItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(
      screen.getByText("Not enough points to redeem this item."),
    ).toBeInTheDocument();
  });

  it("shows loading state", () => {
    mockUseRedemptions.mockReturnValue({
      data: null,
      status: "loading",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(screen.getByText("Loading redemptions...")).toBeInTheDocument();
  });

  it("panel-head shows result count when data loaded", () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([successItem, failedItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    expect(screen.getByText("2 results")).toBeInTheDocument();
  });
});

/**
 * Same signal on the history: sorting now goes to the server, so the click has no immediate effect
 * on the rows. Without a busy marker that reads as nothing happening.
 */
describe("RedemptionHistoryPage — pending feedback while the server re-orders", () => {
  it("marks the table busy while a new query is in flight", () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([successItem]),
      status: "ready",
      isRefreshing: true,
      reload: vi.fn(),
    });

    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });

    expect(screen.getByRole("table")).toHaveAttribute("aria-busy", "true");
  });

  it("leaves the table unmarked once the rows match the query", () => {
    mockUseRedemptions.mockReturnValue({
      data: makeResponse([successItem]),
      status: "ready",
      isRefreshing: false,
      reload: vi.fn(),
    });

    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });

    expect(screen.getByRole("table")).toHaveAttribute("aria-busy", "false");
  });
});
