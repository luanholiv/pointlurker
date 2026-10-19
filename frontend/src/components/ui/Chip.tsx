import type { ReactNode } from "react";

interface ChipProps {
  pressed?: boolean;
  onToggle?: () => void;
  children: ReactNode;
}

const Chip = ({ pressed = false, onToggle, children }: ChipProps) => (
  <button className="chip" aria-pressed={pressed} onClick={onToggle}>
    {children}
  </button>
);

export default Chip;
