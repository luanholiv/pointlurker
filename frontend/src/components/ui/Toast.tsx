import * as Toast from "@radix-ui/react-toast";
import type { Toast as ToastItem } from "./useToast";

interface ToastsProps {
  items: ToastItem[];
  onDismiss?: (id: string) => void;
}

/**
 * `duration` is infinite because the queue owns the timer; Radix only reports a swipe or the
 * Escape key through `onOpenChange`.
 */
const Toasts = ({ items, onDismiss }: ToastsProps) => (
  <Toast.Provider swipeDirection="right" duration={Infinity}>
    {items.map((t) => (
      <Toast.Root
        key={t.id}
        open={t.open}
        onOpenChange={(open) => !open && onDismiss?.(t.id)}
        className="toast"
        data-tone={t.tone}
      >
        <Toast.Title className="s">{t.kicker}</Toast.Title>
        <Toast.Description className="t">{t.msg}</Toast.Description>
      </Toast.Root>
    ))}
    <Toast.Viewport className="toasts" />
  </Toast.Provider>
);

export default Toasts;
