import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { Search, Plus, SearchX, Store } from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  EmptyState,
  PageHead,
  Pill,
  Chip,
  SkeletonGrid,
  useToast,
} from "../../components/ui";
import StoreCard from "../../features/stores/StoreCard";
import AddStoreDialog from "../../features/stores/AddStoreDialog";
import {
  isSingleStoreProvider,
  type StoreProvider,
} from "../../features/stores/storeProviders";
import MissingLabelDialog from "../../features/stores/MissingLabelDialog";
import { useAuth } from "../../features/auth/AuthContext";
import {
  useActivateStore,
  useDeactivateStore,
  useRemoveStore,
  useStoreList,
} from "../../features/stores/useStores";
import type { StoreListItem } from "../../features/stores/storesApi";

type StatusFilter = "all" | "active" | "paused";

const StoresPage = () => {
  const { user } = useAuth();
  const [q, setQ] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");

  // The server narrows the list and counts the totals. The page holds the filter state and
  // renders what comes back — it derives neither the subset nor the counts itself.
  const { stores, totalCount, summary, status, reload } = useStoreList({
    search: q,
    active: statusFilter === "all" ? null : statusFilter === "active",
  });
  const { t } = useTranslation();
  const [dialogOpen, setDialogOpen] = useState(false);
  const [missingLabelPrompt, setMissingLabelPrompt] = useState<{
    storeId: string;
    storeName: string;
    missingLabels: string[];
  } | null>(null);
  const [searchOpen, setSearchOpen] = useState(false);
  const [togglingId, setTogglingId] = useState<string | null>(null);
  const activate = useActivateStore();
  const deactivate = useDeactivateStore();
  const remove = useRemoveStore();
  const navigate = useNavigate();
  const { showToast } = useToast();

  // A store needs its own provider's credential, so the button appears once any is held and the
  // dialog says which one is missing for the provider picked.
  const credentialProviders: StoreProvider[] = [
    ...new Set<StoreProvider>(
      (user?.credentials ?? []).map((credential) => credential.provider),
    ),
  ];
  const hasAnyCredential = credentialProviders.length > 0;
  const addedSingleStoreProviders = stores
    .map((store) => store.provider)
    .filter(isSingleStoreProvider);

  const { activeCount, pausedCount } = summary;
  const ownedCount = activeCount + pausedCount;

  const handleToggle = async (store: StoreListItem) => {
    setTogglingId(store.id);
    const ok = store.isActive
      ? await deactivate.execute(store.id)
      : await activate.execute(store.id);
    setTogglingId(null);
    if (ok) {
      showToast({
        kicker: store.isActive
          ? t("stores.toast.pausedKicker")
          : t("stores.toast.resumedKicker"),
        msg: store.isActive
          ? t("stores.toast.pausedMsg")
          : t("stores.toast.resumedMsg"),
      });
      reload();
    } else {
      showToast({
        kicker: t("stores.toast.errorKicker"),
        msg: t("stores.toast.toggleError"),
        tone: "error",
      });
    }
  };

  const handleRemove = async (id: string) => {
    const ok = await remove.execute(id);
    if (ok) {
      showToast({
        kicker: t("stores.toast.removedKicker"),
        msg: t("stores.toast.removedMsg"),
      });
      reload();
    } else {
      showToast({
        kicker: t("stores.toast.errorKicker"),
        msg: t("stores.toast.removeError"),
        tone: "error",
      });
    }
  };

  return (
    <div className="page">
      <PageHead
        title={t("stores.title")}
        description={t("stores.description")}
        right={
          <>
            <button
              className="btn"
              aria-pressed={searchOpen}
              onClick={() => setSearchOpen((v) => !v)}
            >
              <Search size={13} /> {t("common.search")}
            </button>
            {hasAnyCredential ? (
              <button
                className="btn btn-accent"
                onClick={() => setDialogOpen(true)}
              >
                <Plus size={14} /> {t("stores.addButton")}
              </button>
            ) : (
              <div className="pill warn">
                <span className="dot" />
                {t("stores.configureCredential")}
              </div>
            )}
          </>
        }
      />

      {searchOpen && (
        <div className="panel" style={{ marginBottom: 18 }}>
          <div className="vstack" style={{ gap: 14 }}>
            <div className="search">
              <Search size={15} />
              <input
                className="input"
                placeholder={t("stores.searchPlaceholder")}
                aria-label={t("stores.searchAriaLabel")}
                value={q}
                onChange={(e) => setQ(e.target.value)}
                autoFocus
              />
            </div>
            <div className="hstack" style={{ flexWrap: "wrap", gap: 6 }}>
              <Chip
                pressed={statusFilter === "all"}
                onToggle={() => setStatusFilter("all")}
              >
                {t("stores.filterAll")}
              </Chip>
              <Chip
                pressed={statusFilter === "active"}
                onToggle={() => setStatusFilter("active")}
              >
                {t("stores.filterActive")}
              </Chip>
              <Chip
                pressed={statusFilter === "paused"}
                onToggle={() => setStatusFilter("paused")}
              >
                {t("stores.filterPaused")}
              </Chip>
            </div>
          </div>
        </div>
      )}

      {status === "loading" && (
        <SkeletonGrid label={t("stores.loading")} count={3} media={false} />
      )}

      {status === "error" && (
        <p style={{ color: "var(--bad)" }}>{t("stores.loadError")}</p>
      )}

      {status === "ready" && stores.length === 0 && ownedCount === 0 && (
        <EmptyState
          icon={Store}
          title={t("stores.empty.title")}
          body={
            hasAnyCredential
              ? t("stores.empty.body")
              : t("stores.empty.bodyNoCredential")
          }
          action={
            hasAnyCredential ? (
              <button
                className="btn btn-accent"
                onClick={() => setDialogOpen(true)}
              >
                <Plus size={14} /> {t("stores.addButton")}
              </button>
            ) : (
              <Link className="btn btn-accent" to="/settings">
                {t("stores.empty.openSettings")}
              </Link>
            )
          }
        />
      )}

      {status === "ready" && stores.length === 0 && ownedCount > 0 && (
        <EmptyState
          icon={SearchX}
          tone="filtered"
          title={t("stores.emptyFiltered.title")}
          body={t("stores.emptyFiltered.body")}
          action={
            <button
              className="btn"
              onClick={() => {
                setQ("");
                setStatusFilter("all");
              }}
            >
              {t("common.clearFilters")}
            </button>
          }
        />
      )}

      {status === "ready" && stores.length > 0 && (
        <div className="panel">
          <div className="panel-head">
            <div>
              <div className="t-eyebrow">
                {t("stores.watchedLabel", {
                  filtered: totalCount,
                  total: ownedCount,
                })}
              </div>
            </div>
            <div className="hstack">
              {activeCount > 0 && (
                <Pill tone="ok">
                  {activeCount} {t("stores.active")}
                </Pill>
              )}
              {pausedCount > 0 && (
                <Pill tone="warn">
                  {pausedCount} {t("stores.paused")}
                </Pill>
              )}
            </div>
          </div>
          <div className="grid grid-3 stagger">
            {stores.map((store) => (
              <StoreCard
                key={store.id}
                store={store}
                onView={() => navigate(`/stores/${store.id}`)}
                onToggle={() => handleToggle(store)}
                isLoading={togglingId === store.id}
                onRemove={() => handleRemove(store.id)}
              />
            ))}
          </div>
        </div>
      )}

      <AddStoreDialog
        open={dialogOpen}
        credentialProviders={credentialProviders}
        addedSingleStoreProviders={addedSingleStoreProviders}
        onClose={() => setDialogOpen(false)}
        onCreated={(result) => {
          setDialogOpen(false);
          reload();
          if (result.missingLabels.length > 0) {
            setMissingLabelPrompt(result);
          }
        }}
      />

      {missingLabelPrompt && (
        <MissingLabelDialog
          open={true}
          storeId={missingLabelPrompt.storeId}
          storeName={missingLabelPrompt.storeName}
          missingLabels={missingLabelPrompt.missingLabels}
          onClose={() => setMissingLabelPrompt(null)}
          onSaved={() => setMissingLabelPrompt(null)}
        />
      )}
    </div>
  );
};

export default StoresPage;
