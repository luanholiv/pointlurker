import type { ReactNode } from "react";
import { Eye } from "lucide-react";

export type FeedTone = "ok" | "warn" | "bad" | "accent" | "default";

interface FeedRowProps {
  timestamp: string;
  tone?: FeedTone;
  children: ReactNode;
  onView?: () => void;
}

const FeedRow = ({
  timestamp,
  tone = "default",
  children,
  onView,
}: FeedRowProps) => (
  <div className={`feed-row${tone !== "default" ? ` ${tone}` : ""}`}>
    <div className="ts">{timestamp}</div>
    <div className="mark" />
    <div className="what">{children}</div>
    {onView && (
      <button className="btn btn-sm btn-ghost" onClick={onView}>
        <Eye size={12} />
      </button>
    )}
  </div>
);

export default FeedRow;
