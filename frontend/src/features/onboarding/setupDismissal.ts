import { useCallback, useSyncExternalStore } from "react";

const DISMISSED_ITEM = "pl.setup.dismissed";

const listeners = new Set<() => void>();

const subscribe = (listener: () => void) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};

// Storage can throw (blocked site data, some private windows); unreadable means not dismissed.
const isDismissed = () => {
  try {
    return localStorage.getItem(DISMISSED_ITEM) === "1";
  } catch {
    return false;
  }
};

/**
 * Whether the owner has finished the setup checklist, remembered per browser. Every reader shares
 * one store, so finishing on the page hides the sidebar widget in the same render.
 */
export const useSetupDismissed = (): [boolean, () => void] => {
  const dismissed = useSyncExternalStore(subscribe, isDismissed);

  const dismiss = useCallback(() => {
    try {
      localStorage.setItem(DISMISSED_ITEM, "1");
    } catch {
      // Not remembered, so every reader still sees not-dismissed: nothing to tell them. The
      // checklist comes back on the next visit to `/`.
      return;
    }
    listeners.forEach((listener) => listener());
  }, []);

  return [dismissed, dismiss];
};
