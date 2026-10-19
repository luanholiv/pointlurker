import type { ReactNode } from "react";

export type PillTone = "ok" | "warn" | "bad" | "accent" | "default";

interface PillProps {
  tone?: PillTone;
  children: ReactNode;
  /** Lets callers address a specific pill when several appear on one screen. */
  testId?: string;
}

const Pill = ({ tone = "default", children, testId }: PillProps) => (
  <span
    className={`pill${tone !== "default" ? ` ${tone}` : ""}`}
    data-testid={testId}
  >
    <span className="dot" />
    {children}
  </span>
);

export default Pill;
