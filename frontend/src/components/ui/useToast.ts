import { createContext, useCallback, useContext, useState } from "react";

export interface Toast {
  id: string;
  kicker: string;
  msg: string;
  tone?: "error";
  open: boolean;
}

export type ShowToast = (toast: Omit<Toast, "id" | "open">) => void;

/** Long enough for the exit animation; Radix unmounts the root itself once it has played. */
const EXIT_MS = 500;
const VISIBLE_MS = 8000;

/**
 * Owns the queue for one viewport. Only `ToastProvider` calls this — components reach the
 * queue through `useToast`, so there is exactly one stack per app rather than one per page.
 */
export function useToastState() {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const dismiss = useCallback((id: string) => {
    setToasts((prev) =>
      prev.map((x) => (x.id === id ? { ...x, open: false } : x)),
    );
    setTimeout(() => {
      setToasts((prev) => prev.filter((x) => x.id !== id));
    }, EXIT_MS);
  }, []);

  const showToast = useCallback<ShowToast>(
    (t) => {
      const id = Math.random().toString(36).slice(2);
      setToasts((prev) => [...prev, { ...t, id, open: true }]);
      setTimeout(() => dismiss(id), VISIBLE_MS);
    },
    [dismiss],
  );

  return { toasts, showToast, dismiss };
}

export const ToastContext = createContext<ShowToast | null>(null);

/**
 * Raises a toast from anywhere under the provider.
 *
 * Deliberately throws when the provider is missing rather than returning a no-op: a swallowed
 * `showToast` looks exactly like the silent-failure bug this provider was added to fix, and it
 * would only ever be noticed by the user who hit the error path.
 */
export function useToast() {
  const showToast = useContext(ToastContext);
  if (!showToast) {
    throw new Error("useToast must be used within a ToastProvider");
  }
  return { showToast };
}
