import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Eye, Pause, Play, Trash2 } from "lucide-react";
import Pill from "../../components/ui/Pill";
import ProviderBadge from "./ProviderBadge";
import { formatDate } from "../../i18n/formatters";
import type { StoreListItem } from "./storesApi";

function storeHue(id: string): number {
  let h = 0;
  for (let i = 0; i < id.length; i++) h = (h * 31 + id.charCodeAt(i)) % 360;
  return h;
}

interface StoreCardProps {
  store: StoreListItem;
  onView: () => void;
  onToggle: () => void;
  onRemove: () => void;
  isLoading?: boolean;
}

const StoreCard = ({
  store,
  onView,
  onToggle,
  onRemove,
  isLoading,
}: StoreCardProps) => {
  const { t, i18n } = useTranslation();
  const h = storeHue(store.id);
  const bg = `linear-gradient(135deg, oklch(0.55 0.16 ${h}), oklch(0.40 0.14 ${(h + 40) % 360}))`;
  const [avatarFailed, setAvatarFailed] = useState(false);
  const showAvatarImage = Boolean(store.avatarUrl) && !avatarFailed;

  return (
    <div className="card store-card">
      <div
        className="top"
        style={{
          display: "flex",
          justifyContent: "space-between",
          gap: 8,
          alignItems: "flex-start",
        }}
      >
        <div
          className="hstack"
          style={{
            gap: 12,
            alignItems: "center",
            flex: 1,
            minWidth: 0,
            overflow: "hidden",
          }}
        >
          <div
            className="store-avatar"
            style={{ background: showAvatarImage ? "var(--line-soft)" : bg }}
          >
            {showAvatarImage ? (
              <img
                src={store.avatarUrl ?? undefined}
                alt=""
                referrerPolicy="no-referrer"
                onError={() => setAvatarFailed(true)}
                style={{
                  position: "relative",
                  zIndex: 1,
                  width: "100%",
                  height: "100%",
                  objectFit: "cover",
                  borderRadius: "inherit",
                }}
              />
            ) : (
              <span>{store.displayName.slice(0, 2).toUpperCase()}</span>
            )}
          </div>
          <div className="name t-subheading">{store.displayName}</div>
        </div>
        <div style={{ flexShrink: 0 }}>
          <Pill tone={store.isActive ? "ok" : "warn"}>
            {store.isActive ? t("stores.active") : t("stores.paused")}
          </Pill>
        </div>
      </div>

      <div
        className="stats t-muted"
        style={{
          display: "flex",
          gap: 18,
          padding: "10px 0 8px",
          borderTop: "1px dashed var(--line-soft)",
          marginTop: 4,
        }}
      >
        <span
          style={{
            color: "var(--ink)",
            fontWeight: 500,
            letterSpacing: "-0.01em",
          }}
        >
          {t("stores.itemCount", { count: store.itemCount })}
        </span>
        {store.provider && <ProviderBadge provider={store.provider} />}
        {store.lastSyncedAt && (
          <span>
            <b
              style={{
                color: "var(--ink)",
                fontWeight: 500,
                letterSpacing: "-0.01em",
                marginRight: 4,
              }}
            >
              {formatDate(store.lastSyncedAt, i18n.language, {
                hour: "2-digit",
                minute: "2-digit",
              })}
            </b>
            <span className="t-meta">{t("stores.syncLabel")}</span>
          </span>
        )}
      </div>

      <div
        className="actions"
        style={{
          display: "flex",
          gap: 6,
          flexWrap: "wrap",
          paddingTop: 6,
          borderTop: "1px solid var(--line-soft)",
        }}
      >
        <button className="btn btn-sm" onClick={onView}>
          <Eye size={13} aria-hidden={true} focusable="false" />{" "}
          {t("stores.viewItems")}
        </button>
        <button
          className="btn btn-sm btn-ghost"
          onClick={onToggle}
          disabled={isLoading}
        >
          {store.isActive ? (
            <>
              <Pause size={13} aria-hidden={true} focusable="false" />{" "}
              {t("stores.pause")}
            </>
          ) : (
            <>
              <Play size={13} aria-hidden={true} focusable="false" />{" "}
              {t("stores.resume")}
            </>
          )}
        </button>
        <button
          className="btn btn-sm btn-danger"
          style={{ marginLeft: "auto" }}
          onClick={onRemove}
        >
          <Trash2 size={13} aria-hidden={true} focusable="false" />{" "}
          {t("common.remove")}
        </button>
      </div>
    </div>
  );
};

export default StoreCard;
