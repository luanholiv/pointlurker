import { useCallback } from "react";

/** Load before the reader reaches the bottom, so the next slice is usually already there. */
const ROOT_MARGIN = "400px";

/**
 * Watches an element and calls `onIntersect` when it comes into view. Returned as a ref callback
 * with a cleanup return (React 19), so a sentinel that is conditionally rendered — as this one is,
 * appearing only while more items remain — is re-observed correctly each time it mounts.
 *
 * The sentinel wraps a real button rather than replacing it. The observer just presses it early.
 */
export const useInfiniteScrollSentinel = (
  onIntersect: () => void,
  enabled: boolean,
) => {
  return useCallback(
    (node: HTMLElement | null) => {
      if (!node || !enabled || typeof IntersectionObserver === "undefined") {
        return;
      }

      const observer = new IntersectionObserver(
        (entries) => {
          if (entries.some((entry) => entry.isIntersecting)) {
            onIntersect();
          }
        },
        { rootMargin: ROOT_MARGIN },
      );

      observer.observe(node);

      return () => observer.disconnect();
    },
    [enabled, onIntersect],
  );
};
