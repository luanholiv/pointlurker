import { act, renderHook } from "@testing-library/react";
import { useSetupDismissed } from "./setupDismissal";

const DISMISSED_ITEM = "pl.setup.dismissed";

describe("useSetupDismissed", () => {
  beforeEach(() => localStorage.removeItem(DISMISSED_ITEM));
  afterEach(() => vi.restoreAllMocks());

  it("starts not dismissed", () => {
    const { result } = renderHook(() => useSetupDismissed());
    expect(result.current[0]).toBe(false);
  });

  it("reads a dismissal stored earlier", () => {
    localStorage.setItem(DISMISSED_ITEM, "1");
    const { result } = renderHook(() => useSetupDismissed());
    expect(result.current[0]).toBe(true);
  });

  it("stores the dismissal and tells every reader", () => {
    const first = renderHook(() => useSetupDismissed());
    const second = renderHook(() => useSetupDismissed());

    act(() => first.result.current[1]());

    expect(localStorage.getItem(DISMISSED_ITEM)).toBe("1");
    expect(first.result.current[0]).toBe(true);
    expect(second.result.current[0]).toBe(true);
  });

  it("treats blocked storage as not dismissed, and dismissing does not throw", () => {
    const target =
      localStorage instanceof Storage ? Storage.prototype : localStorage;
    vi.spyOn(target, "getItem").mockImplementation(() => {
      throw new Error("blocked");
    });
    vi.spyOn(target, "setItem").mockImplementation(() => {
      throw new Error("blocked");
    });

    const { result } = renderHook(() => useSetupDismissed());
    expect(result.current[0]).toBe(false);
    expect(() => act(() => result.current[1]())).not.toThrow();
  });
});
