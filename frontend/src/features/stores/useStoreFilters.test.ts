import { renderHook, act } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { createElement } from "react";
import { useStoreFilters } from "./useStoreFilters";
import { DEFAULT_FILTERS } from "./types";

const wrapper =
  (initialSearch = "") =>
  ({ children }: { children: React.ReactNode }) =>
    createElement(
      MemoryRouter,
      { initialEntries: [`/stores/1${initialSearch}`] },
      children,
    );

describe("useStoreFilters", () => {
  it("returns defaults when no URL params", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(),
    });
    expect(result.current.filters).toEqual(DEFAULT_FILTERS);
  });

  it("initializes from URL params", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(
        "?q=sword&avail=0&sub=1&feat=1&cat=Sounds,Emotes&min=100&max=5000&sort=cost&dir=desc",
      ),
    });
    expect(result.current.filters.search).toBe("sword");
    expect(result.current.filters.available).toBe(false);
    expect(result.current.filters.subscriberOnly).toBe(true);
    expect(result.current.filters.featured).toBe(true);
    expect(result.current.filters.categories).toEqual(["Sounds", "Emotes"]);
    expect(result.current.filters.costMin).toBe(100);
    expect(result.current.filters.costMax).toBe(5000);
    expect(result.current.filters.sort).toBe("cost");
    expect(result.current.filters.sortDir).toBe("desc");
  });

  it("ignores invalid sort URL value", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?sort=invalid"),
    });
    expect(result.current.filters.sort).toBe(DEFAULT_FILTERS.sort);
  });

  it("ignores invalid min/max URL values", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?min=notanumber&max=alsowrong"),
    });
    expect(result.current.filters.costMin).toBeNull();
    expect(result.current.filters.costMax).toBeNull();
  });

  it("writes changes to URL when filter is set", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(),
    });
    act(() => {
      result.current.setSearch("fire");
    });
    expect(result.current.filters.search).toBe("fire");
  });

  it("omits default values from URL (available ON is default)", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(),
    });
    act(() => {
      result.current.setSearch("");
    });
    expect(result.current.filters.available).toBe(true);
    expect(result.current.filters.search).toBe("");
  });

  it("resetAll clears all filters to defaults", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?q=sword&avail=0&sub=1&sort=cost"),
    });
    act(() => {
      result.current.resetAll();
    });
    expect(result.current.filters).toEqual(DEFAULT_FILTERS);
  });

  it("hasActiveFilters is false at defaults", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(),
    });
    expect(result.current.hasActiveFilters).toBe(false);
  });

  it("hasActiveFilters is true when available is OFF", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?avail=0"),
    });
    expect(result.current.hasActiveFilters).toBe(true);
  });

  it("hasActiveFilters is true when search is non-empty", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(),
    });
    act(() => {
      result.current.setSearch("sword");
    });
    expect(result.current.hasActiveFilters).toBe(true);
  });

  /**
   * The server picks relevance for a searching request that named no ordering, so an unstated sort
   * has to mean the same thing here — otherwise the dropdown names an ordering the grid is not in.
   */
  it("defaults to relevance while a term is present", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?q=sword"),
    });
    expect(result.current.filters.sort).toBe("relevance");
  });

  it("keeps name as the default with no term", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(),
    });
    expect(result.current.filters.sort).toBe("name");
  });

  it("reads relevance back from the URL", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?q=sword&sort=relevance"),
    });
    expect(result.current.filters.sort).toBe("relevance");
  });

  it("keeps an explicit ordering while searching", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?q=sword&sort=cost"),
    });
    expect(result.current.filters.sort).toBe("cost");
  });

  /**
   * Clearing the box leaves nothing to rank. Were the pair left inconsistent the dropdown would
   * still read "Relevance" while the server answered by name.
   */
  it("drops a relevance ordering when the term is cleared", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?q=sword&sort=relevance"),
    });

    act(() => {
      result.current.setSearch("");
    });

    expect(result.current.filters.sort).toBe("name");
  });

  it("keeps an explicit ordering when the term is cleared", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper("?q=sword&sort=cost"),
    });

    act(() => {
      result.current.setSearch("");
    });

    expect(result.current.filters.sort).toBe("cost");
  });
});
