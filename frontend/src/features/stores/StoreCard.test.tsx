import { fireEvent, render, screen } from "@testing-library/react";
import StoreCard from "./StoreCard";
import type { StoreListItem } from "./storesApi";

const store: StoreListItem = {
  id: "ale_apoka",
  channelId: "5d66c09f5191a2cd52a578b7",
  displayName: "Ale Apoka",
  isActive: true,
  lastSyncedAt: "2026-04-16T10:52:00Z",
  itemCount: 26,
  provider: "streamelements",
};

describe("StoreCard", () => {
  it("renders store display name", () => {
    render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(screen.getByText("Ale Apoka")).toBeInTheDocument();
  });

  it("deterministic avatar color is stable across renders", () => {
    const { container: c1 } = render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    const { container: c2 } = render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    const bg1 = (c1.querySelector(".store-avatar") as HTMLElement).style
      .background;
    const bg2 = (c2.querySelector(".store-avatar") as HTMLElement).style
      .background;
    expect(bg1).toBe(bg2);
  });

  it("active store shows ok pill", () => {
    render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("paused store shows warn pill", () => {
    render(
      <StoreCard
        store={{ ...store, isActive: false }}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(screen.getByText("Paused")).toBeInTheDocument();
  });

  it("renders item count", () => {
    render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(screen.getByText("26 items")).toBeInTheDocument();
  });

  it("does not display channel ID", () => {
    render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(screen.queryByText(/ch ·/)).not.toBeInTheDocument();
    expect(screen.queryByText(store.channelId)).not.toBeInTheDocument();
  });

  it("pill wrapper has flexShrink 0 to prevent card overflow", () => {
    const { container } = render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    const pill = container.querySelector(".pill");
    expect(pill).not.toBeNull();
    expect((pill?.parentElement as HTMLElement).style.flexShrink).toBe("0");
  });

  it("avatar initials derive from display name, not store ID", () => {
    render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(
      screen.getByText(store.displayName.slice(0, 2).toUpperCase()),
    ).toBeInTheDocument();
  });

  it("does not show @ handle or raw store ID as text", () => {
    render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(screen.queryByText(/@/)).not.toBeInTheDocument();
    expect(screen.queryByText(store.id)).not.toBeInTheDocument();
  });

  it("pause/resume button is disabled when isLoading", () => {
    render(
      <StoreCard
        store={store}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
        isLoading
      />,
    );
    expect(screen.getByRole("button", { name: /pause/i })).toBeDisabled();
  });

  it("renders avatar image when avatarUrl is set", () => {
    const { container } = render(
      <StoreCard
        store={{ ...store, avatarUrl: "https://cdn.example.com/avatar.png" }}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    const img = container.querySelector(
      ".store-avatar img",
    ) as HTMLImageElement;
    expect(img).not.toBeNull();
    expect(img.src).toBe("https://cdn.example.com/avatar.png");
    expect(img.getAttribute("referrerpolicy")).toBe("no-referrer");
  });

  it("shows initials when avatarUrl is null", () => {
    const { container } = render(
      <StoreCard
        store={{ ...store, avatarUrl: null }}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(container.querySelector(".store-avatar img")).toBeNull();
    expect(screen.getByText("AL")).toBeInTheDocument();
  });

  it("falls back to initials when avatar image fails to load", () => {
    const { container } = render(
      <StoreCard
        store={{ ...store, avatarUrl: "https://cdn.example.com/broken.png" }}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    const img = container.querySelector(
      ".store-avatar img",
    ) as HTMLImageElement;
    fireEvent.error(img);
    expect(container.querySelector(".store-avatar img")).toBeNull();
    expect(screen.getByText("AL")).toBeInTheDocument();
  });

  it("names the provider the store comes from", () => {
    render(
      <StoreCard
        store={{ ...store, provider: "gaupoints" }}
        onView={vi.fn()}
        onToggle={vi.fn()}
        onRemove={vi.fn()}
      />,
    );
    expect(screen.getByTestId("provider-badge")).toHaveTextContent("GauPoints");
  });
});
