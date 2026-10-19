import { render, screen, fireEvent } from "@testing-library/react";
import StoreItemFilters from "./StoreItemFilters";
import type { StoreItemFilters as Filters } from "./types";
import { DEFAULT_FILTERS } from "./types";
import type { StoreFilterOptions } from "./useStoreItemFacets";

const defaultOptions: StoreFilterOptions = {
  categories: ["Sounds", "Emotes", "Actions"],
  costBounds: { min: 100, max: 5000 },
};

const renderFilters = (
  filtersOverride: Partial<Filters> = {},
  optionsOverride: Partial<StoreFilterOptions> = {},
  handlers: Partial<{
    onSearch: (v: string) => void;
    onAvailable: (v: boolean) => void;
    onSubscriberOnly: (v: boolean) => void;
    onFeatured: (v: boolean) => void;
    onCategories: (v: string[]) => void;
    onCostMin: (v: number | null) => void;
    onCostMax: (v: number | null) => void;
    onSort: (v: Filters["sort"]) => void;
    onSortDir: (v: Filters["sortDir"]) => void;
    onResetAll: () => void;
  }> = {},
) =>
  render(
    <StoreItemFilters
      filters={{ ...DEFAULT_FILTERS, ...filtersOverride }}
      options={{ ...defaultOptions, ...optionsOverride }}
      onSearch={handlers.onSearch ?? vi.fn()}
      onAvailable={handlers.onAvailable ?? vi.fn()}
      onSubscriberOnly={handlers.onSubscriberOnly ?? vi.fn()}
      onFeatured={handlers.onFeatured ?? vi.fn()}
      onCategories={handlers.onCategories ?? vi.fn()}
      onCostMin={handlers.onCostMin ?? vi.fn()}
      onCostMax={handlers.onCostMax ?? vi.fn()}
      onSort={handlers.onSort ?? vi.fn()}
      onSortDir={handlers.onSortDir ?? vi.fn()}
      onResetAll={handlers.onResetAll ?? vi.fn()}
      hasActiveFilters={false}
    />,
  );

describe("StoreItemFilters", () => {
  it("renders search input", () => {
    renderFilters();
    expect(
      screen.getByRole("searchbox", { name: /search items/i }),
    ).toBeInTheDocument();
  });

  it("renders all toggle buttons", () => {
    renderFilters();
    expect(
      screen.getByRole("button", { name: "Available" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Sub Only" }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("button", { name: "Featured" }),
    ).toBeInTheDocument();
  });

  it("renders sort field and direction selects", () => {
    renderFilters();
    expect(
      screen.getByRole("combobox", { name: /sort field/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("combobox", { name: /sort direction/i }),
    ).toBeInTheDocument();
  });

  /**
   * Relevance is only an ordering while there is something to rank, so it appears with the term and
   * not before it.
   */
  it("offers relevance only while a term is present", () => {
    renderFilters({ search: "sword", sort: "relevance" });

    expect(
      screen.getByRole("option", { name: "Relevance" }),
    ).toBeInTheDocument();
  });

  it("omits relevance with an empty search box", () => {
    renderFilters();

    expect(
      screen.queryByRole("option", { name: "Relevance" }),
    ).not.toBeInTheDocument();
  });

  /**
   * Relevance is the ordering a searching view already has, so it is not something to announce as
   * an active filter — the chip would ask the reader to dismiss the state they are in.
   */
  it("does not chip relevance while searching", () => {
    renderFilters({ search: "sword", sort: "relevance" });

    expect(screen.queryByText(/Sort: Relevance/i)).not.toBeInTheDocument();
  });

  it("chips an ordering chosen against the default", () => {
    renderFilters({ search: "sword", sort: "cost" });

    expect(screen.getByText(/Sort: Cost/i)).toBeInTheDocument();
  });

  it("toggles Available and calls handler", () => {
    const onAvailable = vi.fn();
    renderFilters({}, {}, { onAvailable });
    fireEvent.click(screen.getByRole("button", { name: "Available" }));
    expect(onAvailable).toHaveBeenCalledWith(false);
  });

  it("toggles Sub Only and calls handler", () => {
    const onSubscriberOnly = vi.fn();
    renderFilters({}, {}, { onSubscriberOnly });
    fireEvent.click(screen.getByRole("button", { name: "Sub Only" }));
    expect(onSubscriberOnly).toHaveBeenCalledWith(true);
  });

  it("toggles Featured and calls handler", () => {
    const onFeatured = vi.fn();
    renderFilters({}, {}, { onFeatured });
    fireEvent.click(screen.getByRole("button", { name: "Featured" }));
    expect(onFeatured).toHaveBeenCalledWith(true);
  });

  it("category buttons populated from options", () => {
    renderFilters();
    expect(screen.getByRole("button", { name: "Sounds" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Emotes" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Actions" })).toBeInTheDocument();
  });

  it("clicking a category calls onCategories with that category added", () => {
    const onCategories = vi.fn();
    renderFilters({}, {}, { onCategories });
    fireEvent.click(screen.getByRole("button", { name: "Sounds" }));
    expect(onCategories).toHaveBeenCalledWith(["Sounds"]);
  });

  it("clicking an active category removes it", () => {
    const onCategories = vi.fn();
    renderFilters({ categories: ["Sounds"] }, {}, { onCategories });
    fireEvent.click(screen.getByRole("button", { name: "Sounds" }));
    expect(onCategories).toHaveBeenCalledWith([]);
  });

  it("cost range inputs present", () => {
    renderFilters();
    expect(
      screen.getByRole("spinbutton", { name: /minimum cost/i }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("spinbutton", { name: /maximum cost/i }),
    ).toBeInTheDocument();
  });

  it("active chips shown for non-default filters", () => {
    render(
      <StoreItemFilters
        filters={{
          ...DEFAULT_FILTERS,
          subscriberOnly: true,
          categories: ["Sounds"],
        }}
        options={defaultOptions}
        onSearch={vi.fn()}
        onAvailable={vi.fn()}
        onSubscriberOnly={vi.fn()}
        onFeatured={vi.fn()}
        onCategories={vi.fn()}
        onCostMin={vi.fn()}
        onCostMax={vi.fn()}
        onSort={vi.fn()}
        onSortDir={vi.fn()}
        onResetAll={vi.fn()}
        hasActiveFilters={true}
      />,
    );
    const activeFiltersRegion = screen.getByLabelText("Active filters");
    expect(activeFiltersRegion).toHaveTextContent("Sub Only");
    expect(activeFiltersRegion).toHaveTextContent("Sounds");
  });

  it("chip dismiss clears that filter", () => {
    const onSubscriberOnly = vi.fn();
    render(
      <StoreItemFilters
        filters={{ ...DEFAULT_FILTERS, subscriberOnly: true }}
        options={defaultOptions}
        onSearch={vi.fn()}
        onAvailable={vi.fn()}
        onSubscriberOnly={onSubscriberOnly}
        onFeatured={vi.fn()}
        onCategories={vi.fn()}
        onCostMin={vi.fn()}
        onCostMax={vi.fn()}
        onSort={vi.fn()}
        onSortDir={vi.fn()}
        onResetAll={vi.fn()}
        hasActiveFilters={true}
      />,
    );
    fireEvent.click(
      screen.getByRole("button", { name: /remove filter sub only/i }),
    );
    expect(onSubscriberOnly).toHaveBeenCalledWith(false);
  });

  it('"Reset all" visible when hasActiveFilters', () => {
    render(
      <StoreItemFilters
        filters={{ ...DEFAULT_FILTERS, subscriberOnly: true }}
        options={defaultOptions}
        onSearch={vi.fn()}
        onAvailable={vi.fn()}
        onSubscriberOnly={vi.fn()}
        onFeatured={vi.fn()}
        onCategories={vi.fn()}
        onCostMin={vi.fn()}
        onCostMax={vi.fn()}
        onSort={vi.fn()}
        onSortDir={vi.fn()}
        onResetAll={vi.fn()}
        hasActiveFilters={true}
      />,
    );
    expect(
      screen.getByRole("button", { name: /reset all/i }),
    ).toBeInTheDocument();
  });

  it('"Reset all" not visible when no active filters', () => {
    renderFilters();
    expect(
      screen.queryByRole("button", { name: /reset all/i }),
    ).not.toBeInTheDocument();
  });

  it('"Reset all" calls onResetAll', () => {
    const onResetAll = vi.fn();
    render(
      <StoreItemFilters
        filters={{ ...DEFAULT_FILTERS, subscriberOnly: true }}
        options={defaultOptions}
        onSearch={vi.fn()}
        onAvailable={vi.fn()}
        onSubscriberOnly={vi.fn()}
        onFeatured={vi.fn()}
        onCategories={vi.fn()}
        onCostMin={vi.fn()}
        onCostMax={vi.fn()}
        onSort={vi.fn()}
        onSortDir={vi.fn()}
        onResetAll={onResetAll}
        hasActiveFilters={true}
      />,
    );
    fireEvent.click(screen.getByRole("button", { name: /reset all/i }));
    expect(onResetAll).toHaveBeenCalled();
  });
});
