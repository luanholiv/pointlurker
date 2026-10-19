import { withViewTransition } from "./viewTransition";

type StartViewTransition = (cb: () => void) => { finished: Promise<void> };

const doc = document as unknown as {
  startViewTransition?: StartViewTransition;
};

const setReducedMotion = (reduce: boolean) => {
  window.matchMedia = vi.fn().mockReturnValue({ matches: reduce });
};

afterEach(() => {
  delete doc.startViewTransition;
  // @ts-expect-error jsdom ships no matchMedia; tests add one and take it away again.
  delete window.matchMedia;
});

describe("withViewTransition", () => {
  it("applies the update directly when the browser has no View Transitions", async () => {
    const update = vi.fn();
    const before = vi.fn();
    await withViewTransition(update, before);
    expect(update).toHaveBeenCalledOnce();
    expect(before).not.toHaveBeenCalled();
  });

  it("runs the update inside a transition, after the setup callback", async () => {
    const order: string[] = [];
    doc.startViewTransition = vi.fn((cb: () => void) => {
      order.push("start");
      cb();
      return { finished: Promise.resolve() };
    });
    setReducedMotion(false);

    await withViewTransition(
      () => order.push("update"),
      () => order.push("before"),
    );

    expect(order).toEqual(["before", "start", "update"]);
  });

  it("skips the transition when the reader prefers reduced motion", async () => {
    doc.startViewTransition = vi.fn();
    setReducedMotion(true);
    const update = vi.fn();

    await withViewTransition(update);

    expect(update).toHaveBeenCalledOnce();
    expect(doc.startViewTransition).not.toHaveBeenCalled();
  });
});
