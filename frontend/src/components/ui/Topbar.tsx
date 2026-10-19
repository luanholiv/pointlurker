import { Sun, Moon } from "lucide-react";
import { useLocation, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useTheme } from "../../hooks/useTheme";
import type { ReactNode } from "react";
import MobileNav from "./MobileNav";

interface TopbarProps {
  actions?: ReactNode;
}

const Topbar = ({ actions }: TopbarProps) => {
  const { theme, toggle } = useTheme();
  const location = useLocation();
  const params = useParams<{ id?: string }>();
  const { t } = useTranslation();

  function buildCrumbs(pathname: string, storeId?: string): string[] {
    const brand = t("common.brandName");
    if (pathname === "/") return [brand, t("topbar.getStarted")];
    if (pathname === "/stores") return [brand, t("navigation.stores")];
    if (pathname.startsWith("/stores/"))
      return [
        brand,
        t("navigation.stores"),
        storeId ?? pathname.split("/")[2] ?? "",
      ];
    if (pathname === "/wishlist") return [brand, t("navigation.wishlist")];
    if (pathname === "/redemptions")
      return [brand, t("navigation.redemptions")];
    if (pathname === "/settings") return [brand, t("navigation.settings")];
    return [brand];
  }

  const crumbs = buildCrumbs(location.pathname, params.id);

  return (
    <div className="topbar">
      <a href="#main-content" className="skip-link">
        {t("topbar.skipToContent")}
      </a>
      <MobileNav />
      <div className="crumbs">
        {crumbs.map((c, i) => (
          <span key={i} style={{ display: "contents" }}>
            {i > 0 && <span className="sep">/</span>}
            <span className={i === crumbs.length - 1 ? "here" : ""}>{c}</span>
          </span>
        ))}
      </div>
      <div className="top-actions">
        {actions}
        <button
          className="iconbtn"
          onClick={(e) => {
            const r = e.currentTarget.getBoundingClientRect();
            toggle({ x: r.left + r.width / 2, y: r.top + r.height / 2 });
          }}
          aria-label={
            theme === "dark"
              ? t("topbar.switchToLight")
              : t("topbar.switchToDark")
          }
        >
          {theme === "dark" ? (
            <Sun size={15} aria-hidden={true} focusable="false" />
          ) : (
            <Moon size={15} aria-hidden={true} focusable="false" />
          )}
        </button>
      </div>
    </div>
  );
};

export default Topbar;
