import { renderHook, act } from "@testing-library/react";
import { useToastState } from "./useToast";

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

describe("useToastState", () => {
  it("starts with empty toasts", () => {
    const { result } = renderHook(() => useToastState());
    expect(result.current.toasts).toHaveLength(0);
  });

  it("adds toast to array on showToast", () => {
    const { result } = renderHook(() => useToastState());
    act(() => {
      result.current.showToast({ kicker: "sync", msg: "Item found" });
    });
    expect(result.current.toasts).toHaveLength(1);
    expect(result.current.toasts[0].kicker).toBe("sync");
    expect(result.current.toasts[0].msg).toBe("Item found");
  });

  it("assigns unique id to each toast", () => {
    const { result } = renderHook(() => useToastState());
    act(() => {
      result.current.showToast({ kicker: "a", msg: "1" });
      result.current.showToast({ kicker: "b", msg: "2" });
    });
    const ids = result.current.toasts.map((t) => t.id);
    expect(new Set(ids).size).toBe(2);
  });

  it("removes toast after 8500ms (8000ms display + 500ms exit)", () => {
    const { result } = renderHook(() => useToastState());
    act(() => {
      result.current.showToast({ kicker: "k", msg: "m" });
    });
    expect(result.current.toasts).toHaveLength(1);
    act(() => {
      vi.advanceTimersByTime(8500);
    });
    expect(result.current.toasts).toHaveLength(0);
  });

  it("does not remove toast before 8000ms", () => {
    const { result } = renderHook(() => useToastState());
    act(() => {
      result.current.showToast({ kicker: "k", msg: "m" });
    });
    act(() => {
      vi.advanceTimersByTime(7999);
    });
    expect(result.current.toasts).toHaveLength(1);
  });

  it("dismiss closes a toast at once and removes it after the exit window", () => {
    const { result } = renderHook(() => useToastState());
    act(() => {
      result.current.showToast({ kicker: "a", msg: "swiped" });
    });
    const { id } = result.current.toasts[0];

    act(() => result.current.dismiss(id));
    expect(result.current.toasts[0].open).toBe(false);

    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(result.current.toasts).toHaveLength(0);
  });
});
