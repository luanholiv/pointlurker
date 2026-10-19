import { describe, it, expect, vi } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import type { ReactElement } from "react";
import { server } from "../../test/server";
import { renderWithToasts } from "../../test/renderWithToasts";
import WishlistGroupedList from "./WishlistGroupedList";
import type { WishlistGroupedList as WishlistGroupedListType } from "./wishlistApi";
import { setAccessToken } from "../auth/authStorage";

/** Only `items` varies between these fixtures, so the rest of the envelope is spread in. */
const pageMeta = { totalCount: 0, offset: 0, limit: 0 };

const renderWithRouter = (ui: ReactElement) =>
  renderWithToasts(ui, { route: "/wishlist" });

/** A backend error apiClient will parse into an ApiError, i.e. one carrying `code`. */
const problem = (status: number, body: Record<string, unknown>) =>
  HttpResponse.json(body, {
    status,
    headers: { "Content-Type": "application/problem+json" },
  });

const oneEntry = (): WishlistGroupedListType => ({
  ...pageMeta,
  items: [makeStore({ entries: [makeEntry()] })],
});

const toasts = () => document.querySelectorAll("li.toast");

const makeStore = (overrides?: object) => ({
  storeId: "store-1",
  storeName: "xQc Store",
  isActive: true,
  lastSyncedAt: "2026-01-01T00:00:00Z",
  entries: [],
  ...overrides,
});

const makeEntry = (overrides?: object) => ({
  id: "entry-1",
  storeItemId: "item-1",
  itemName: "VIP Badge",
  itemCost: 100,
  isAvailable: true,
  notifyOnAvailable: false,
  autoRedeem: false,
  isBlocked: false,
  missingLabels: [] as string[],
  createdAt: "2026-01-01T00:00:00Z",
  ...overrides,
});

const emptyData: WishlistGroupedListType = { ...pageMeta, items: [] };

describe("WishlistGroupedList", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  it("shows empty state when no stores", () => {
    renderWithRouter(
      <WishlistGroupedList data={emptyData} onChanged={vi.fn()} />,
    );
    expect(screen.getByText(/your wishlist is empty/i)).toBeInTheDocument();
  });

  it("renders store name and Active pill", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ isActive: true })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByText("xQc Store")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("renders Paused pill when store is inactive", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ isActive: false })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByText("Paused")).toBeInTheDocument();
  });

  it("renders wishlist entry with name and cost", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry()] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByText("VIP Badge")).toBeInTheDocument();
    expect(screen.getByText(/100 pts/)).toBeInTheDocument();
  });

  it("shows Available pill for available items", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry({ isAvailable: true })] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByText("Available")).toBeInTheDocument();
  });

  it("shows Unavailable pill for unavailable items", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry({ isAvailable: false })] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByText("Unavailable")).toBeInTheDocument();
  });

  it("shows Notify chip when notifyOnAvailable is true", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry({ notifyOnAvailable: true })] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByText("Notify")).toBeInTheDocument();
  });

  it("shows Auto-redeem chip when autoRedeem is true", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry({ autoRedeem: true })] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByText("Auto-redeem")).toBeInTheDocument();
  });

  it("shows blocked banner when isBlocked is true", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [
        makeStore({
          entries: [
            makeEntry({ isBlocked: true, missingLabels: ["Twitch Username"] }),
          ],
        }),
      ],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);
    expect(screen.getByTestId(`wishlist-blocked-entry-1`)).toBeInTheDocument();
    expect(screen.getByText(/twitch username/i)).toBeInTheDocument();
  });

  it("renders entry thumbnail and store avatar when URLs are set", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [
        makeStore({
          avatarUrl: "https://cdn.example.com/avatar.png",
          entries: [
            makeEntry({ thumbnailUrl: "https://cdn.example.com/thumb.png" }),
          ],
        }),
      ],
    };
    const { container } = renderWithRouter(
      <WishlistGroupedList data={data} onChanged={vi.fn()} />,
    );
    const imgs = Array.from(container.querySelectorAll("img")).map(
      (i) => (i as HTMLImageElement).src,
    );
    expect(imgs).toContain("https://cdn.example.com/avatar.png");
    expect(imgs).toContain("https://cdn.example.com/thumb.png");
  });

  it("renders no images when thumbnail and avatar URLs are absent", () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry()] })],
    };
    const { container } = renderWithRouter(
      <WishlistGroupedList data={data} onChanged={vi.fn()} />,
    );
    expect(container.querySelector("img")).toBeNull();
  });

  it("calls onChanged after successful remove", async () => {
    const onChanged = vi.fn();
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry()] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={onChanged} />);

    await userEvent.click(screen.getByRole("button", { name: /remove/i }));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
  });

  it("opens edit modal when Edit button is clicked", async () => {
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry()] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={vi.fn()} />);

    await userEvent.click(screen.getByRole("button", { name: /edit/i }));

    await waitFor(() =>
      expect(screen.getByText(/VIP Badge/i)).toBeInTheDocument(),
    );
  });

  it("does not call onChanged if remove fails", async () => {
    server.use(
      http.delete(
        "http://localhost/api/wishlist/:id",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    const onChanged = vi.fn();
    const data: WishlistGroupedListType = {
      ...pageMeta,
      items: [makeStore({ entries: [makeEntry()] })],
    };
    renderWithRouter(<WishlistGroupedList data={data} onChanged={onChanged} />);

    await userEvent.click(screen.getByRole("button", { name: /remove/i }));

    await waitFor(() => expect(onChanged).not.toHaveBeenCalled());
  });
});

/**
 * Both affordances have to report their failure: the entry fetch behind Edit and the delete behind
 * Remove each swallow the error if it is caught and returned, leaving the button looking inert
 * rather than failed. These lock in that the failure is visible, and that a successful click stays
 * silent.
 */
describe("WishlistGroupedList — reporting a failed action", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  it("raises an error toast and keeps the modal shut when the entry fetch fails", async () => {
    server.use(
      http.get(
        "http://localhost/api/wishlist/:id",
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    renderWithRouter(
      <WishlistGroupedList data={oneEntry()} onChanged={vi.fn()} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /edit/i }));

    const toast = await screen.findByText("Failed to load wishlist entry");
    expect(toast.closest("[data-tone]")).toHaveAttribute("data-tone", "error");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("shows the backend's reason when the entry fetch is refused with a code", async () => {
    server.use(
      http.get("http://localhost/api/wishlist/:id", () =>
        problem(404, { code: "wishlist.entry_not_found" }),
      ),
    );
    renderWithRouter(
      <WishlistGroupedList data={oneEntry()} onChanged={vi.fn()} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /edit/i }));

    expect(
      await screen.findByText("Wishlist entry not found."),
    ).toBeInTheDocument();
  });

  it("raises an error toast when the remove fails", async () => {
    server.use(
      http.delete("http://localhost/api/wishlist/:id", () =>
        problem(404, { code: "wishlist.entry_not_found" }),
      ),
    );
    const onChanged = vi.fn();
    renderWithRouter(
      <WishlistGroupedList data={oneEntry()} onChanged={onChanged} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /remove/i }));

    const toast = await screen.findByText("Wishlist entry not found.");
    expect(toast.closest("[data-tone]")).toHaveAttribute("data-tone", "error");
    expect(onChanged).not.toHaveBeenCalled();
  });

  it("opens the modal and stays silent when the entry fetch succeeds", async () => {
    renderWithRouter(
      <WishlistGroupedList data={oneEntry()} onChanged={vi.fn()} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /edit/i }));

    expect(await screen.findByRole("dialog")).toBeInTheDocument();
    expect(toasts()).toHaveLength(0);
  });

  it("stays silent when the remove succeeds", async () => {
    const onChanged = vi.fn();
    renderWithRouter(
      <WishlistGroupedList data={oneEntry()} onChanged={onChanged} />,
    );

    await userEvent.click(screen.getByRole("button", { name: /remove/i }));

    await waitFor(() => expect(onChanged).toHaveBeenCalled());
    expect(toasts()).toHaveLength(0);
  });
});
