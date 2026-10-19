import { useState } from "react";
import type { CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Edit2, Heart, Store, Trash2 } from "lucide-react";
import { Link } from "react-router";
import Pill from "../../components/ui/Pill";
import Chip from "../../components/ui/Chip";
import { EmptyState, useToast } from "../../components/ui";
import { formatDate, formatList, formatNumber } from "../../i18n/formatters";
import { useRemoveWishlist, useWishlistEntryDetail } from "./useWishlist";
import type {
  WishlistEntryDetail,
  WishlistGroupedList as WishlistGroupedListType,
} from "./wishlistApi";
import WishlistEntryModal from "./WishlistEntryModal";

// Renders a CDN image, or nothing if the URL is absent or the image fails to
// load. Wishlist cards have no gradient/glyph placeholder, so the fallback is
// simply the existing text-only layout.
const RemoteImage = ({
  url,
  style,
}: {
  url?: string | null;
  style: CSSProperties;
}) => {
  const [failed, setFailed] = useState(false);
  if (!url || failed) {
    return null;
  }
  return (
    <img
      src={url}
      alt=""
      referrerPolicy="no-referrer"
      onError={() => setFailed(true)}
      style={style}
    />
  );
};

type WishlistGroupedListProps = {
  data: WishlistGroupedListType;
  onChanged: () => void;
};

const WishlistGroupedList = ({ data, onChanged }: WishlistGroupedListProps) => {
  const { t, i18n } = useTranslation();
  const [editEntry, setEditEntry] = useState<WishlistEntryDetail | null>(null);
  const [editModalOpen, setEditModalOpen] = useState(false);
  const remove = useRemoveWishlist();
  const loadEntry = useWishlistEntryDetail();
  const { showToast } = useToast();

  const reportFailure = (msg: string) =>
    showToast({ kicker: t("wishlist.toast.errorKicker"), msg, tone: "error" });

  const handleEdit = async (entryId: string) => {
    const outcome = await loadEntry.execute(entryId);
    if (!outcome.ok) {
      reportFailure(outcome.message);
      return;
    }
    setEditEntry(outcome.entry);
    setEditModalOpen(true);
  };

  const handleRemove = async (entryId: string) => {
    const outcome = await remove.execute(entryId);
    if (!outcome.ok) {
      reportFailure(outcome.message);
      return;
    }
    onChanged();
  };

  const handleEditSaved = () => {
    setEditModalOpen(false);
    setEditEntry(null);
    onChanged();
  };

  if (data.items.length === 0) {
    return (
      <EmptyState
        icon={Heart}
        title={t("wishlist.empty.title")}
        body={t("wishlist.empty.body")}
        action={
          <Link className="btn btn-accent" to="/stores">
            <Store size={14} aria-hidden={true} focusable="false" />
            {t("wishlist.empty.cta")}
          </Link>
        }
      />
    );
  }

  return (
    <>
      <div className="vstack" style={{ gap: 20 }}>
        {data.items.map((store) => (
          <div className="panel" key={store.storeId}>
            <div className="panel-head">
              <div className="hstack" style={{ gap: 12, alignItems: "center" }}>
                <RemoteImage
                  url={store.avatarUrl}
                  style={{
                    width: 36,
                    height: 36,
                    borderRadius: "var(--r-3)",
                    objectFit: "cover",
                    flexShrink: 0,
                  }}
                />
                <div>
                  <div className="t-heading">{store.storeName}</div>
                  {store.lastSyncedAt && (
                    <div className="t-meta t-muted" style={{ marginTop: 2 }}>
                      {t("wishlist.synced", {
                        date: formatDate(store.lastSyncedAt, i18n.language, {
                          dateStyle: "short",
                          timeStyle: "short",
                        }),
                      })}
                    </div>
                  )}
                </div>
              </div>
              <Pill tone={store.isActive ? "ok" : "warn"}>
                {store.isActive ? t("stores.active") : t("stores.paused")}
              </Pill>
            </div>

            <div className="grid grid-3">
              {store.entries.map((entry) => (
                <div className="card" key={entry.id}>
                  <div className="vstack" style={{ gap: 8 }}>
                    <RemoteImage
                      url={entry.thumbnailUrl}
                      style={{
                        width: "100%",
                        height: 90,
                        objectFit: "cover",
                        borderRadius: "var(--r-3)",
                        border: "1px solid var(--line-soft)",
                      }}
                    />
                    <div
                      className="between"
                      style={{ alignItems: "flex-start" }}
                    >
                      <div className="title t-subheading">{entry.itemName}</div>
                      <Pill tone={entry.isAvailable ? "ok" : "bad"}>
                        {entry.isAvailable
                          ? t("wishlist.available")
                          : t("wishlist.unavailable")}
                      </Pill>
                    </div>

                    <div className="t-mono t-muted">
                      {formatNumber(entry.itemCost, i18n.language)}{" "}
                      {t("common.pts")}
                    </div>

                    {(entry.notifyOnAvailable || entry.autoRedeem) && (
                      <div
                        className="hstack"
                        style={{ flexWrap: "wrap", gap: 6 }}
                      >
                        {entry.notifyOnAvailable && (
                          <Chip>{t("wishlist.notify")}</Chip>
                        )}
                        {entry.autoRedeem && (
                          <Chip>{t("wishlist.autoRedeem")}</Chip>
                        )}
                        {entry.autoRedeem && (
                          <span
                            data-testid={`wishlist-quota-${entry.id}`}
                            className="t-small t-muted"
                          >
                            {entry.desiredRedemptions === null
                              ? t("wishlist.quotaProgressUnlimited", {
                                  used: entry.quotaUsed,
                                })
                              : t("wishlist.quotaProgress", {
                                  used: entry.quotaUsed,
                                  desired: entry.desiredRedemptions,
                                })}
                          </span>
                        )}
                        {/* Auto-redeem stays on when the count is met, so the chip alone cannot
                            say why nothing more is being redeemed. */}
                        {entry.autoRedeem &&
                          entry.desiredRedemptions !== null &&
                          entry.quotaUsed >= entry.desiredRedemptions && (
                            <Chip>{t("wishlist.quotaReached")}</Chip>
                          )}
                      </div>
                    )}

                    {entry.isBlocked && (
                      <div
                        data-testid={`wishlist-blocked-${entry.id}`}
                        className="t-small"
                        style={{
                          background: "var(--warn-soft)",
                          border: "1px solid var(--warn)",
                          borderRadius: "var(--r-2)",
                          padding: "6px 12px",
                          color: "var(--ink-2)",
                        }}
                      >
                        {t("wishlist.blocked", {
                          labels: formatList(
                            entry.missingLabels,
                            i18n.language,
                          ),
                        })}{" "}
                        <Link to={`/stores/${store.storeId}#profile`}>
                          {t("wishlist.fillProfile")}
                        </Link>
                      </div>
                    )}

                    <div
                      className="hstack"
                      style={{
                        gap: 6,
                        paddingTop: 6,
                        borderTop: "1px solid var(--line-soft)",
                      }}
                    >
                      <button
                        className="btn btn-sm"
                        onClick={() => handleEdit(entry.id)}
                      >
                        <Edit2 size={12} /> {t("wishlist.edit")}
                      </button>
                      <button
                        className="btn btn-sm btn-danger"
                        style={{ marginLeft: "auto" }}
                        onClick={() => handleRemove(entry.id)}
                      >
                        <Trash2 size={12} /> {t("wishlist.remove")}
                      </button>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>

      {editEntry && (
        <WishlistEntryModal
          open={editModalOpen}
          onClose={() => {
            setEditModalOpen(false);
            setEditEntry(null);
          }}
          onSaved={handleEditSaved}
          mode="edit"
          itemName={editEntry.itemName}
          itemCost={editEntry.itemCost}
          allowMessages={editEntry.allowMessages}
          existingEntry={editEntry}
          storeId={editEntry.storeId}
          isBlocked={editEntry.isBlocked}
          missingLabels={editEntry.missingLabels}
        />
      )}
    </>
  );
};

export default WishlistGroupedList;
