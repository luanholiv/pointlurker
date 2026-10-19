/**
 * Locale-blind card rendering.
 *
 * A card holding English literals, or formatting dates and numbers with the *browser* locale (or a
 * hardcoded "en-US"), stays English with en-US separators when the app switches to pt-BR. Neither
 * catalogCompleteness nor deadKeys can catch that — both work on catalog keys, not on what a
 * component actually renders. This drives the components through a real locale switch instead.
 */
import { render, screen } from "@testing-library/react";
import i18n from "../../i18n";
import ItemCard from "./ItemCard";
import StoreCard from "./StoreCard";
import type { StoreItemDetail, StoreListItem } from "./storesApi";

const store: StoreListItem = {
  id: "ale_apoka",
  channelId: "5d66c09f5191a2cd52a578b7",
  displayName: "Ale Apoka",
  isActive: true,
  lastSyncedAt: "2026-04-16T10:52:00Z",
  itemCount: 26,
  provider: "streamelements" as const,
};

const item: StoreItemDetail = {
  id: "item1",
  providerItemId: "se1",
  name: "Desert Eagle Tilted",
  description: "Cool skin",
  cost: 9000,
  categoryName: "SKIN",
  subscriberOnly: true,
  allowMessages: false,
  quantityTotal: 10,
  quantityCurrent: 4,
  isAvailable: true,
  thumbnailUrl: null,
  wishlistEntryId: null,
  providerCreatedAt: "2026-01-01T00:00:00Z",
};

const renderStoreCard = () =>
  render(
    <StoreCard
      store={store}
      onView={vi.fn()}
      onToggle={vi.fn()}
      onRemove={vi.fn()}
    />,
  );

describe("card rendering under pt-BR", () => {
  beforeAll(async () => {
    await i18n.changeLanguage("pt-BR");
  });

  afterAll(async () => {
    await i18n.changeLanguage("en-US");
  });

  it("translates StoreCard status and actions", () => {
    renderStoreCard();
    expect(screen.getByText("Ativa")).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: /Ver itens/ }),
    ).toBeInTheDocument();
    expect(screen.queryByText("Active")).not.toBeInTheDocument();
    expect(screen.queryByText("View items")).not.toBeInTheDocument();
  });

  it("formats the store item count with the pt-BR plural", () => {
    renderStoreCard();
    expect(screen.getByText("26 itens")).toBeInTheDocument();
  });

  it("formats lastSyncedAt with the app locale, not the browser locale", () => {
    renderStoreCard();
    // pt-BR uses a 24-hour clock, so the app locale reaching the formatter means no
    // AM/PM marker. The exact hour depends on the runner's timezone, so match shape.
    expect(screen.getByText(/^\d{2}:\d{2}$/)).toBeInTheDocument();
    expect(screen.queryByText(/AM|PM/)).not.toBeInTheDocument();
  });

  it("translates ItemCard availability and wishlist action", () => {
    render(<ItemCard item={item} onWishlist={vi.fn()} />);
    expect(screen.getByText("Disponível")).toBeInTheDocument();
    expect(screen.getByText("Só sub")).toBeInTheDocument();
    expect(screen.queryByText("Available")).not.toBeInTheDocument();
    expect(screen.queryByText("Sub Only")).not.toBeInTheDocument();
  });

  it("formats the item cost with pt-BR separators instead of a hardcoded en-US", () => {
    render(<ItemCard item={item} onWishlist={vi.fn()} />);
    // The bug: `item.cost.toLocaleString("en-US")` rendered "9,000" in every locale.
    expect(screen.getByText("9.000")).toBeInTheDocument();
    expect(screen.queryByText("9,000")).not.toBeInTheDocument();
  });
});
