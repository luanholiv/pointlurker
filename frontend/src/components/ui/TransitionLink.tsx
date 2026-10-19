import type { ReactNode } from "react";
import { Link, useLocation, useNavigate } from "react-router";
import { isPlainLeftClick, withViewTransition } from "../../lib/viewTransition";

interface TransitionLinkProps {
  to: string;
  className?: string;
  children: ReactNode;
}

/**
 * A link that changes route inside a View Transition, like the sidebar navigation does. A link
 * to the page already shown does not animate: there is nothing to transition to.
 */
const TransitionLink = ({ to, className, children }: TransitionLinkProps) => {
  const navigate = useNavigate();
  const { pathname, hash } = useLocation();

  return (
    <Link
      to={to}
      className={className}
      onClick={(e) => {
        if (!isPlainLeftClick(e) || pathname + hash === to) return;
        e.preventDefault();
        void withViewTransition(() => navigate(to));
      }}
    >
      {children}
    </Link>
  );
};

export default TransitionLink;
