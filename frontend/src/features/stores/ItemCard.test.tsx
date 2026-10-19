import { fireEvent, render, screen } from "@testing-library/react";
import ItemCard from "./ItemCard";
import type { StoreItemDetail } from "./storesApi";

const availableItem: StoreItemDetail = {
  id: "item1",
  providerItemId: "se1",
  name: "Desert Eagle Tilted",
  description: "Cool skin",
  cost: 9000,
  categoryName: "SKIN",
  subscriberOnly: false,
  allowMessages: false,
  quantityTotal: 10,
  quantityCurrent: -1,
  isAvailable: true,
  thumbnailUrl: null,
  wishlistEntryId: null,
  providerCreatedAt: "2026-01-01T00:00:00Z",
};

const unavailableItem: StoreItemDetail = {
  ...availableItem,
  id: "item2",
  name: "Rare Item",
  isAvailable: false,
  quantityCurrent: 0,
};

describe("ItemCard", () => {
  it("renders item name", () => {
    render(<ItemCard item={availableItem} onWishlist={vi.fn()} />);
    expect(screen.getByText("Desert Eagle Tilted")).toBeInTheDocument();
  });

  it("available item shows ok pill", () => {
    render(<ItemCard item={availableItem} onWishlist={vi.fn()} />);
    expect(screen.getByText("Available")).toBeInTheDocument();
  });

  it("unavailable item shows bad pill", () => {
    render(<ItemCard item={unavailableItem} onWishlist={vi.fn()} />);
    expect(screen.getByText("Unavailable")).toBeInTheDocument();
  });

  it("category glyph rendered in media area", () => {
    render(<ItemCard item={availableItem} onWishlist={vi.fn()} />);
    expect(screen.getByText("◆")).toBeInTheDocument();
  });

  it("shows price in pts", () => {
    render(<ItemCard item={availableItem} onWishlist={vi.fn()} />);
    expect(screen.getByText("9,000")).toBeInTheDocument();
  });

  it("does not show stock when unlimited (-1)", () => {
    render(<ItemCard item={availableItem} onWishlist={vi.fn()} />);
    expect(screen.queryByText(/Stock:/)).not.toBeInTheDocument();
  });

  it("shows stock when finite", () => {
    render(
      <ItemCard
        item={{ ...availableItem, quantityCurrent: 5 }}
        onWishlist={vi.fn()}
      />,
    );
    expect(screen.getByText("Stock: 5 / 10")).toBeInTheDocument();
  });

  it("renders thumbnail image when thumbnailUrl is set", () => {
    const { container } = render(
      <ItemCard
        item={{
          ...availableItem,
          thumbnailUrl: "https://cdn.example.com/thumb.png",
        }}
        onWishlist={vi.fn()}
      />,
    );
    const img = container.querySelector(".item-media img") as HTMLImageElement;
    expect(img).not.toBeNull();
    expect(img.src).toBe("https://cdn.example.com/thumb.png");
    expect(img.getAttribute("referrerpolicy")).toBe("no-referrer");
    expect(container.querySelector(".item-media-glyph")).toBeNull();
  });

  it("shows gradient glyph when thumbnailUrl is null", () => {
    const { container } = render(
      <ItemCard
        item={{ ...availableItem, thumbnailUrl: null }}
        onWishlist={vi.fn()}
      />,
    );
    expect(container.querySelector(".item-media img")).toBeNull();
    expect(container.querySelector(".item-media-glyph")).not.toBeNull();
  });

  it("falls back to gradient glyph when thumbnail fails to load", () => {
    const { container } = render(
      <ItemCard
        item={{
          ...availableItem,
          thumbnailUrl: "https://cdn.example.com/broken.png",
        }}
        onWishlist={vi.fn()}
      />,
    );
    const img = container.querySelector(".item-media img") as HTMLImageElement;
    fireEvent.error(img);
    expect(container.querySelector(".item-media img")).toBeNull();
    expect(container.querySelector(".item-media-glyph")).not.toBeNull();
  });

  it("plays the wishlist burst when the item joins the wishlist", () => {
    const { rerender } = render(
      <ItemCard item={availableItem} onWishlist={vi.fn()} />,
    );
    const button = screen.getByRole("button", { name: /add to wishlist/i });
    expect(button).not.toHaveClass("pop");

    rerender(<ItemCard item={availableItem} onWishlist={vi.fn()} wishlisted />);
    expect(button).toHaveClass("pop");
  });

  it("does not play the burst for an item already on the wishlist", () => {
    render(<ItemCard item={availableItem} onWishlist={vi.fn()} wishlisted />);
    expect(screen.getByRole("button", { name: /wishlisted/i })).not.toHaveClass(
      "pop",
    );
  });
});
