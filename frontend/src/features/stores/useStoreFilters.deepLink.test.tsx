import { renderHook, act } from "@testing-library/react";
import { MemoryRouter, useLocation } from "react-router";
import type { ReactNode } from "react";
import { useStoreFilters, serializeFilters } from "./useStoreFilters";
import { DEFAULT_FILTERS } from "./types";

/**
 * Filter state lives in the URL, and one serializer produces both the browser URL and the API
 * query string. That is what makes a filtered view shareable: the link a user copies reproduces
 * the view, and the server understands the same parameters without translation.
 */
describe("store filters as a deep link", () => {
  const wrapper =
    (entry: string) =>
    ({ children }: { children: ReactNode }) => (
      <MemoryRouter initialEntries={[entry]}>{children}</MemoryRouter>
    );

  it("restores every filter from a URL", () => {
    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(
        "/stores/1?q=gold&avail=0&sub=1&feat=1&cat=Emotes,Badges&min=100&max=500&sort=cost&dir=desc",
      ),
    });

    expect(result.current.filters).toEqual({
      search: "gold",
      available: false,
      subscriberOnly: true,
      featured: true,
      categories: ["Emotes", "Badges"],
      costMin: 100,
      costMax: 500,
      sort: "cost",
      sortDir: "desc",
    });
  });

  it("round-trips: the URL it writes is the URL it reads back", () => {
    const filters = {
      search: "gold",
      available: false,
      subscriberOnly: true,
      featured: false,
      categories: ["Emotes"],
      costMin: 100,
      costMax: null,
      sort: "stock" as const,
      sortDir: "desc" as const,
    };

    const { result } = renderHook(() => useStoreFilters(), {
      wrapper: wrapper(`/stores/1?${serializeFilters(filters).toString()}`),
    });

    expect(result.current.filters).toEqual(filters);
  });

  it("puts a changed filter into the URL, so reloading reproduces the view", () => {
    const Probe = () => ({
      filters: useStoreFilters(),
      location: useLocation(),
    });

    const { result } = renderHook(Probe, { wrapper: wrapper("/stores/1") });

    act(() => result.current.filters.setSort("cost"));

    expect(result.current.location.search).toContain("sort=cost");
  });

  it("omits defaults, keeping a shared link to the default view clean", () => {
    expect(serializeFilters(DEFAULT_FILTERS).toString()).toBe("");
  });

  /**
   * The parameter names are the contract with the backend: the endpoint answers to exactly these,
   * so a change here silently stops matching the server rather than failing loudly.
   */
  it("uses the parameter names the API answers to", () => {
    const query = serializeFilters({
      ...DEFAULT_FILTERS,
      search: "x",
      available: false,
      subscriberOnly: true,
      featured: true,
      categories: ["A"],
      costMin: 1,
      costMax: 2,
      sort: "date",
      sortDir: "desc",
    });

    expect([...query.keys()].sort()).toEqual([
      "avail",
      "cat",
      "dir",
      "feat",
      "max",
      "min",
      "q",
      "sort",
      "sub",
    ]);
  });
});
