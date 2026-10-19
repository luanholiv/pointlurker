import { renderHook, act } from "@testing-library/react";
import { useTheme } from "./useTheme";

beforeEach(() => {
  localStorage.clear();
  document.documentElement.removeAttribute("data-theme");
});

describe("useTheme", () => {
  it("defaults to dark when no localStorage key", () => {
    const { result } = renderHook(() => useTheme());
    expect(result.current.theme).toBe("dark");
  });

  it("reads initial theme from pl.theme localStorage key", () => {
    localStorage.setItem("pl.theme", "light");
    const { result } = renderHook(() => useTheme());
    expect(result.current.theme).toBe("light");
  });

  it("sets data-theme attribute on documentElement", () => {
    localStorage.setItem("pl.theme", "dark");
    renderHook(() => useTheme());
    expect(document.documentElement.getAttribute("data-theme")).toBe("dark");
  });

  it("toggle switches dark to light and sets data-theme attribute", () => {
    localStorage.setItem("pl.theme", "dark");
    const { result } = renderHook(() => useTheme());
    act(() => result.current.toggle());
    expect(result.current.theme).toBe("light");
    expect(document.documentElement.getAttribute("data-theme")).toBe("light");
  });

  it("toggle switches light to dark and sets data-theme attribute", () => {
    localStorage.setItem("pl.theme", "light");
    const { result } = renderHook(() => useTheme());
    act(() => result.current.toggle());
    expect(result.current.theme).toBe("dark");
    expect(document.documentElement.getAttribute("data-theme")).toBe("dark");
  });

  it("a toggle from a point reveals the new theme through a view transition", async () => {
    const doc = document as unknown as {
      startViewTransition?: (cb: () => void) => { finished: Promise<void> };
    };
    let switching = false;
    let finish!: () => void;
    doc.startViewTransition = vi.fn((cb: () => void) => {
      switching = document.documentElement.hasAttribute("data-theme-switching");
      cb();
      return { finished: new Promise<void>((r) => (finish = r)) };
    });
    window.matchMedia = vi.fn().mockReturnValue({ matches: false });
    localStorage.setItem("pl.theme", "dark");
    const { result } = renderHook(() => useTheme());

    act(() => result.current.toggle({ x: 10, y: 20 }));

    expect(switching).toBe(true);
    expect(document.documentElement.style.getPropertyValue("--reveal-x")).toBe(
      "10px",
    );
    expect(document.documentElement.getAttribute("data-theme")).toBe("light");
    expect(result.current.theme).toBe("light");

    await act(async () => finish());
    expect(document.documentElement.hasAttribute("data-theme-switching")).toBe(
      false,
    );

    delete doc.startViewTransition;
    // @ts-expect-error jsdom ships no matchMedia; the test added one and takes it away again.
    delete window.matchMedia;
  });

  it("persists theme to pl.theme localStorage key", () => {
    localStorage.setItem("pl.theme", "dark");
    const { result } = renderHook(() => useTheme());
    act(() => result.current.toggle());
    expect(localStorage.getItem("pl.theme")).toBe("light");
  });
});
