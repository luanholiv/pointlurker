import type { ReactNode } from "react";
import Brand from "./Brand";
import NavList from "./NavList";

interface SidebarProps {
  /** Pinned below the navigation. */
  footer?: ReactNode;
}

const Sidebar = ({ footer }: SidebarProps) => (
  <aside className="sidebar">
    <Brand />
    <NavList animated={true} />
    <div className="sidebar-foot">{footer}</div>
  </aside>
);

export default Sidebar;
