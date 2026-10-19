import type { MouseEvent } from "react";
import { flushSync } from "react-dom";

/** A click the router may take over: a modified or middle click belongs to the browser (new tab, new window). */
export function isPlainLeftClick(e: MouseEvent): boolean {
  return (
    !e.defaultPrevented &&
    e.button === 0 &&
    !e.metaKey &&
    !e.ctrlKey &&
    !e.shiftKey &&
    !e.altKey
  );
}

function prefersReducedMotion(): boolean {
  return (
    window.matchMedia?.("(prefers-reduced-motion: reduce)").matches ?? false
  );
}

/**
 * Runs a React state update inside a View Transition. `flushSync` commits the update before the
 * browser captures the new state; without it the snapshot is taken of the old DOM and nothing
 * animates. Browsers without the API, and readers who asked for reduced motion, get the update
 * with no transition.
 */
export function withViewTransition(
  update: () => void,
  before?: () => void,
): Promise<void> {
  if (
    typeof document.startViewTransition !== "function" ||
    prefersReducedMotion()
  ) {
    update();
    return Promise.resolve();
  }
  before?.();
  return document.startViewTransition(() => flushSync(update)).finished;
}
