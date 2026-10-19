import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import WishlistEntryModal from "./WishlistEntryModal";
import { setAccessToken } from "../auth/authStorage";
import type { WishlistEntryDetail } from "./wishlistApi";

const makeExistingEntry = (
  overrides?: Partial<WishlistEntryDetail>,
): WishlistEntryDetail => ({
  id: "entry-1",
  storeItemId: "item-1",
  itemName: "VIP Badge",
  itemCost: 100,
  isAvailable: true,
  storeName: "xQc Store",
  storeId: "store-1",
  notifyOnAvailable: true,
  autoRedeem: false,
  desiredRedemptions: 1,
  quotaUsed: 0,
  allowMessages: false,
  redemptionMessage: null,
  isBlocked: false,
  missingLabels: [],
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
  ...overrides,
});

describe("WishlistEntryModal", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  it("renders nothing when open is false", () => {
    const { container } = render(
      <WishlistEntryModal
        open={false}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
      />,
    );
    expect(container.firstChild).toBeNull();
  });

  it("renders Add to Wishlist button in add mode", () => {
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
        itemName="VIP Badge"
        itemCost={100}
      />,
    );
    expect(
      screen.getByRole("button", { name: /add to wishlist/i }),
    ).toBeInTheDocument();
  });

  it("renders Save Changes button in edit mode", () => {
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="edit"
        existingEntry={makeExistingEntry()}
      />,
    );
    expect(
      screen.getByRole("button", { name: /save changes/i }),
    ).toBeInTheDocument();
  });

  it("calls onClose when Cancel is clicked", async () => {
    const onClose = vi.fn();
    render(
      <WishlistEntryModal
        open={true}
        onClose={onClose}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
      />,
    );
    await userEvent.click(screen.getByRole("button", { name: /cancel/i }));
    expect(onClose).toHaveBeenCalled();
  });

  it("calls onSaved after successful add", async () => {
    const onSaved = vi.fn();
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={onSaved}
        mode="add"
        storeItemId="item-1"
        itemName="VIP Badge"
        allowMessages={false}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
  });

  it("calls onSaved after successful edit", async () => {
    const onSaved = vi.fn();
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={onSaved}
        mode="edit"
        existingEntry={makeExistingEntry()}
        allowMessages={false}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: /save changes/i }),
    );
    await waitFor(() => expect(onSaved).toHaveBeenCalled());
  });

  it("shows blocked banner when isBlocked is true", () => {
    render(
      <MemoryRouter>
        <WishlistEntryModal
          open={true}
          onClose={vi.fn()}
          onSaved={vi.fn()}
          mode="edit"
          existingEntry={makeExistingEntry({
            isBlocked: true,
            missingLabels: ["Discord Tag"],
          })}
          isBlocked={true}
          missingLabels={["Discord Tag"]}
        />
      </MemoryRouter>,
    );
    expect(screen.getByTestId("wishlist-blocked-banner")).toBeInTheDocument();
  });

  it("shows input fields when auto-redeem is toggled and definitions exist", async () => {
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
      />,
    );
    await userEvent.click(screen.getByLabelText("Auto-redeem when available"));
    expect(
      screen.getByLabelText("Auto-redeem when available"),
    ).toBeInTheDocument();
  });

  it("hides the desired-redemption count until auto-redeem is on", async () => {
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
      />,
    );
    expect(
      screen.queryByTestId("desired-redemptions-field"),
    ).not.toBeInTheDocument();

    await userEvent.click(screen.getByLabelText("Auto-redeem when available"));

    expect(screen.getByLabelText("How many to redeem")).toHaveValue(1);
  });

  it("sends the desired count it was given", async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post("http://localhost/api/wishlist", async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ id: "entry-1" }, { status: 201 });
      }),
    );
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
      />,
    );

    await userEvent.click(screen.getByLabelText("Auto-redeem when available"));
    await userEvent.clear(screen.getByLabelText("How many to redeem"));
    await userEvent.type(screen.getByLabelText("How many to redeem"), "3");
    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );

    await waitFor(() => expect(body?.desiredRedemptions).toBe(3));
  });

  it("sends null only when no limit is chosen", async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post("http://localhost/api/wishlist", async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ id: "entry-1" }, { status: 201 });
      }),
    );
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
      />,
    );

    await userEvent.click(screen.getByLabelText("Auto-redeem when available"));
    await userEvent.click(screen.getByLabelText("No limit"));
    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );

    await waitFor(() => expect(body?.desiredRedemptions).toBeNull());
  });

  it("sends zero rather than unlimited when the count is cleared", async () => {
    let body: Record<string, unknown> | undefined;
    server.use(
      http.post("http://localhost/api/wishlist", async ({ request }) => {
        body = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ id: "entry-1" }, { status: 201 });
      }),
    );
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
      />,
    );

    await userEvent.click(screen.getByLabelText("Auto-redeem when available"));
    await userEvent.clear(screen.getByLabelText("How many to redeem"));
    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );

    await waitFor(() => expect(body?.desiredRedemptions).toBe(0));
  });

  it("opens an unlimited entry with the no-limit switch already on", () => {
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="edit"
        existingEntry={makeExistingEntry({
          autoRedeem: true,
          desiredRedemptions: null,
        })}
      />,
    );

    expect(screen.getByLabelText("No limit")).toBeChecked();
    expect(
      screen.queryByLabelText("How many to redeem"),
    ).not.toBeInTheDocument();
  });

  it("shows redemption message textarea when allowMessages is true", () => {
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
        allowMessages={true}
      />,
    );
    expect(screen.getByLabelText(/redemption message/i)).toBeInTheDocument();
  });

  it("does not call onSaved and shows nothing when API fails on add", async () => {
    server.use(
      http.post(
        "http://localhost/api/wishlist",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    const onSaved = vi.fn();
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={onSaved}
        mode="add"
        storeItemId="item-1"
        allowMessages={false}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );
    await waitFor(() => expect(onSaved).not.toHaveBeenCalled());
  });

  it("attaches a rejected redemption message to its own input", async () => {
    server.use(
      http.post("http://localhost/api/wishlist", () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            code: "validation.failed",
            params: {
              errors: [
                {
                  field: "redemptionMessage",
                  code: "validation.too_long",
                  params: { field: "redemptionMessage", max: 1000 },
                },
              ],
            },
          },
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
        allowMessages={true}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );

    await waitFor(() =>
      expect(
        screen.getByLabelText("Redemption message (optional)"),
      ).toHaveAccessibleDescription(
        "Value is too long (maximum 1000 characters).",
      ),
    );
  });

  // `storeItemId` has no input on this form, so its message has to name itself at form level.
  it("names a field it has no input for", async () => {
    server.use(
      http.post("http://localhost/api/wishlist", () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            code: "validation.failed",
            params: {
              errors: [
                {
                  field: "storeItemId",
                  code: "validation.required",
                  params: { field: "storeItemId" },
                },
              ],
            },
          },
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );
    render(
      <WishlistEntryModal
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        mode="add"
        storeItemId="item-1"
        allowMessages={false}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: /add to wishlist/i }),
    );

    expect(
      await screen.findByText("Item: This field is required."),
    ).toBeInTheDocument();
  });
});
