import type { ReactNode } from "react";
import Toasts from "./Toast";
import { ToastContext, useToastState } from "./useToast";

/**
 * The app's single toast host. Mounted once in `App`, so any component can raise a toast
 * without owning queue state or rendering a viewport of its own.
 *
 * The viewport renders unconditionally — an empty stack still mounts the live region, which is
 * what lets assistive tech announce the first toast instead of missing it to a late insertion.
 */
const ToastProvider = ({ children }: { children: ReactNode }) => {
  const { toasts, showToast, dismiss } = useToastState();

  return (
    <ToastContext.Provider value={showToast}>
      {children}
      <Toasts items={toasts} onDismiss={dismiss} />
    </ToastContext.Provider>
  );
};

export default ToastProvider;
