import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import RedemptionHistoryPage from "./RedemptionHistoryPage";
import { clearAccessToken, setAccessToken } from "../auth/authStorage";

// No mock on useRedemptions — tests the full data flow through the real hook + MSW

describe("RedemptionHistoryPage integration (real hook + MSW)", () => {
  beforeEach(() => setAccessToken("test-token"));
  afterEach(() => clearAccessToken());

  it("renders redemptions fetched from the API", async () => {
    server.use(
      http.get("http://localhost/api/redemptions", () =>
        HttpResponse.json({
          items: [
            {
              id: "r1",
              status: "Completed",
              storeItemName: "VIP Badge",
              storeName: "xQc Store",
              attemptedAt: "2026-01-01T10:00:00Z",
              submittedAt: "2026-01-01T10:00:01Z",
              settledAt: "2026-01-01T10:00:02Z",
              failureCode: null,
              failureContext: null,
            },
          ],
          totalCount: 1,
          page: 1,
          pageSize: 20,
        }),
      ),
    );
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await waitFor(() =>
      expect(screen.getByText("VIP Badge")).toBeInTheDocument(),
    );
    expect(screen.getByText("xQc Store")).toBeInTheDocument();
  });

  it("shows empty state when API returns no items", async () => {
    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await waitFor(() =>
      expect(
        screen.getByRole("heading", { name: "No redemptions yet" }),
      ).toBeInTheDocument(),
    );
  });

  it("filter chip change triggers refetch with status query param", async () => {
    const capturedUrls: string[] = [];
    server.use(
      http.get("http://localhost/api/redemptions", ({ request }) => {
        capturedUrls.push(request.url);
        return HttpResponse.json({
          items: [],
          totalCount: 0,
          page: 1,
          pageSize: 20,
        });
      }),
    );

    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });

    await waitFor(() =>
      expect(
        screen.queryByText("Loading redemptions..."),
      ).not.toBeInTheDocument(),
    );

    await userEvent.click(screen.getByRole("button", { name: "Search" }));
    await userEvent.click(screen.getByRole("button", { name: "Failed" }));

    await waitFor(() =>
      expect(
        capturedUrls.some((u) => u.includes("status=SubmissionFailed")),
      ).toBe(true),
    );
  });
});

/**
 * Sorting is a request, not a re-arrangement of the rows on screen: the server orders the whole
 * history before slicing.
 *
 * These assert the client's half of that: the click becomes a request carrying the new ordering,
 * and it returns to the first page, because "first" means first *overall* rather than first on
 * the page being viewed. That the server really orders the entire result set is proven where it can
 * be — against PostgreSQL, in `RedemptionOrderingTests` — since a browser has no way to create the
 * twenty-plus redemptions the property needs.
 */
describe("RedemptionHistoryPage — sorting is a request, not a re-arrangement", () => {
  beforeEach(() => setAccessToken("test-token"));
  afterEach(() => clearAccessToken());

  const capture = (urls: string[]) =>
    server.use(
      http.get("http://localhost/api/redemptions", ({ request }) => {
        urls.push(request.url);
        return HttpResponse.json({
          // One row is enough: the table — and so the sortable column headers — renders only
          // when the history is non-empty.
          items: [
            {
              id: "r1",
              status: "Completed",
              storeItemName: "VIP Badge",
              storeName: "xQc Store",
              attemptedAt: "2026-01-01T10:00:00Z",
              submittedAt: null,
              settledAt: null,
              failureCode: null,
              failureContext: null,
            },
          ],
          totalCount: 200,
          offset: 0,
          limit: 20,
        });
      }),
    );

  it("sends the chosen column to the server", async () => {
    const urls: string[] = [];
    capture(urls);

    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await waitFor(() => expect(urls.length).toBeGreaterThan(0));

    await userEvent.click(screen.getByRole("columnheader", { name: "Store" }));

    await waitFor(() => expect(urls.at(-1)).toContain("sort=storeName"));
  });

  it("returns to the first page when the ordering changes", async () => {
    const urls: string[] = [];
    capture(urls);

    render(<RedemptionHistoryPage />, { wrapper: MemoryRouter });
    await waitFor(() => expect(urls.length).toBeGreaterThan(0));

    // Move off page 1 first, so the reset has something to undo.
    await userEvent.click(screen.getByRole("button", { name: "Next" }));
    await waitFor(() => expect(urls.at(-1)).toContain("offset=20"));

    await userEvent.click(screen.getByRole("columnheader", { name: "Store" }));

    // A later page of the old order is not a page of the new one.
    await waitFor(() => expect(urls.at(-1)).toContain("offset=0"));
  });
});
