import type { LucideIcon } from "lucide-react";
import type { ReactNode } from "react";

/**
 * `first-run`: nothing exists yet, so the action creates the first one (accent tile).
 * `filtered`: things exist but the search or filters hide them, so the action clears them.
 * `idle`: nothing to do and nothing to offer.
 */
export type EmptyStateTone = "first-run" | "filtered" | "idle";

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  body?: ReactNode;
  tone?: EmptyStateTone;
  action?: ReactNode;
}

const EmptyState = ({
  icon: Icon,
  title,
  body,
  tone = "first-run",
  action,
}: EmptyStateProps) => (
  <div className="empty" data-tone={tone}>
    <div className="empty-tile">
      <Icon size={20} aria-hidden={true} focusable="false" />
    </div>
    <h2 className="empty-title t-subheading">{title}</h2>
    {body && <p className="empty-body t-small">{body}</p>}
    {action && <div className="empty-action">{action}</div>}
  </div>
);

export default EmptyState;
