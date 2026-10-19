import { useNavigate } from "react-router";
import { RefreshCw } from "lucide-react";
import { useTranslation } from "react-i18next";
import { KPI, FeedRow, PageHead, Pill } from "../../components/ui";
import { useWishlistList } from "../../features/wishlist/useWishlist";
import { useStoreList } from "../../features/stores/useStores";
import { useRedemptions } from "../../features/redemptions/useRedemptions";
import { formatNumber } from "../../i18n/formatters";

const HomePage = () => {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const { summary } = useStoreList();
  const { data: wishlist } = useWishlistList();
  // Asked for, not filtered out of a loaded list: the server decides what "available" means.
  const { data: availableWishlist } = useWishlistList({ availableOnly: true });
  const { data: redemptions } = useRedemptions({
    status: null,
    from: null,
    to: null,
    page: 1,
    pageSize: 50,
  });

  const storeCount = summary.activeCount + summary.pausedCount;
  // Counting entries across the returned rows, not narrowing them: a row is a store group, so the
  // envelope's total counts stores rather than entries.
  const wishlistCount = wishlist
    ? wishlist.items.reduce((a, s) => a + s.entries.length, 0)
    : 0;
  const availableNow = availableWishlist
    ? availableWishlist.items.flatMap((s) => s.entries)
    : [];
  const redemptionCount = redemptions ? redemptions.totalCount : 0;

  return (
    <div className="page">
      <PageHead
        title={
          <>
            {t("home.title")}{" "}
            <em style={{ fontStyle: "italic", color: "var(--accent)" }}>
              {t("home.titleEm")}
            </em>
          </>
        }
        description={t("home.noActivity")}
        right={
          <>
            <Pill tone="ok">{t("home.workerOnline")}</Pill>
            <button className="btn">
              <RefreshCw size={13} /> {t("home.forceSync")}
            </button>
          </>
        }
      />

      <div className="grid grid-4" style={{ marginBottom: 18 }}>
        <KPI
          label={t("home.kpiStores")}
          value={String(storeCount).padStart(2, "0")}
          delta={t("home.deltaThisWeek")}
        />
        <KPI
          label={t("home.kpiWishlist")}
          value={String(wishlistCount).padStart(2, "0")}
          delta={t("home.kpiAvailableCount", { count: availableNow.length })}
        />
        <KPI
          label={t("home.kpiRedemptions")}
          value={String(redemptionCount).padStart(2, "0")}
          delta={t("home.deltaVsPrev")}
        />
        <KPI
          label={t("home.kpiSyncLatency")}
          value={
            <>
              —
              <span className="t-subheading t-muted" style={{ marginLeft: 4 }}>
                {t("home.unitSeconds")}
              </span>
            </>
          }
          delta="—"
        />
      </div>

      <div
        className="grid"
        style={{ gridTemplateColumns: "1.35fr 1fr", gap: 18 }}
      >
        <div className="panel">
          <div className="panel-head">
            <div>
              <div className="t-eyebrow">{t("home.liveFeedEyebrow")}</div>
              <div className="t-heading" style={{ marginTop: 4 }}>
                {t("home.activityHeading")}
              </div>
            </div>
            <Pill tone="accent">{t("home.streaming")}</Pill>
          </div>
          <div className="feed">
            <FeedRow
              timestamp="—"
              tone="default"
              onView={() => navigate("/stores")}
            >
              {t("home.noActivity")}
            </FeedRow>
          </div>
        </div>

        <div className="vstack" style={{ gap: 18 }}>
          <div className="panel">
            <div className="panel-head">
              <div>
                <div className="t-eyebrow">{t("home.nextCycleIn")}</div>
                <div className="t-hero" style={{ marginTop: 6 }}>
                  —:——
                </div>
              </div>
            </div>
            <div className="vstack" style={{ gap: 6 }}>
              <div className="between">
                <span className="t-eyebrow">{t("home.lastCycle")}</span>
                <span className="t-meta">—</span>
              </div>
              <div className="between">
                <span className="t-eyebrow">{t("home.storesLabel")}</span>
                <span className="t-meta">
                  {storeCount} / {storeCount}
                </span>
              </div>
              <div className="between">
                <span className="t-eyebrow">{t("home.newItems")}</span>
                <span className="t-meta">0</span>
              </div>
              <div className="between">
                <span className="t-eyebrow">{t("home.autoRedeem")}</span>
                <span className="t-meta">—</span>
              </div>
            </div>
          </div>

          <div className="panel">
            <div className="panel-head">
              <div>
                <div className="t-eyebrow">{t("home.wishlistReady")}</div>
                <div className="t-heading" style={{ marginTop: 4 }}>
                  {t("home.availableNow")}
                </div>
              </div>
            </div>
            <div className="vstack" style={{ gap: 8 }}>
              {availableNow.length === 0 ? (
                <p className="t-small t-muted">{t("home.noAvailableItems")}</p>
              ) : (
                availableNow.map((entry) => (
                  <div key={entry.id} className="between">
                    <div>
                      <div>{entry.itemName}</div>
                      <div className="t-meta t-muted">
                        {formatNumber(entry.itemCost, i18n.language)}{" "}
                        {t("common.pts")}
                      </div>
                    </div>
                    <button className="btn btn-sm btn-accent">
                      {t("home.redeem")}
                    </button>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default HomePage;
