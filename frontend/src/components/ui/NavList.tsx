import { Store, Heart, History, Settings } from "lucide-react";
import { NavLink, useLocation, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { isPlainLeftClick, withViewTransition } from "../../lib/viewTransition";

interface NavListProps {
  /** Called after a link is followed, so a drawer can close itself. */
  onNavigate?: () => void;
  /** Animate the route change. Off in the drawer, which is already animating its own exit. */
  animated?: boolean;
}

const NavList = ({ onNavigate, animated = false }: NavListProps) => {
  const location = useLocation();
  const navigate = useNavigate();
  const { t } = useTranslation();

  const NAV = [
    { num: "01", label: t("navigation.stores"), to: "/stores", icon: Store },
    {
      num: "02",
      label: t("navigation.wishlist"),
      to: "/wishlist",
      icon: Heart,
    },
    {
      num: "03",
      label: t("navigation.redemptions"),
      to: "/redemptions",
      icon: History,
    },
    {
      num: "04",
      label: t("navigation.settings"),
      to: "/settings",
      icon: Settings,
    },
  ];

  return (
    <nav className="nav">
      <div className="nav-group">{t("common.workspace")}</div>
      {NAV.map((item) => {
        const active =
          location.pathname === item.to ||
          location.pathname.startsWith(`${item.to}/`);

        return (
          <NavLink
            key={item.to}
            to={item.to}
            className="nav-item"
            aria-current={active ? "page" : undefined}
            onClick={(e) => {
              onNavigate?.();
              if (!animated || active || !isPlainLeftClick(e)) return;
              e.preventDefault();
              void withViewTransition(() => navigate(item.to));
            }}
          >
            {active && <span className="nav-ind" aria-hidden={true} />}
            <span className="nav-num">{item.num}</span>
            <item.icon size={15} aria-hidden={true} focusable="false" />
            <span>{item.label}</span>
            {active && (
              <span
                className="nav-dot"
                style={{ background: "var(--accent)" }}
              />
            )}
          </NavLink>
        );
      })}
    </nav>
  );
};

export default NavList;
