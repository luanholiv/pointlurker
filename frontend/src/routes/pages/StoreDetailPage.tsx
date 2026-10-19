import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { ArrowLeft, PackageOpen, Search, SearchX } from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  EmptyState,
  NotificationDot,
  PageHead,
  Pill,
  SkeletonGrid,
  Tabs,
  useToast,
} from "../../components/ui";
import { useHashTab } from "../../hooks/useHashTab";
import { useStoreDetail } from "../../features/stores/useStores";
import type { StoreItemDetail } from "../../features/stores/storesApi";
import ItemCard from "../../features/stores/ItemCard";
import WishlistEntryModal from "../../features/wishlist/WishlistEntryModal";
import { useWishlistEntryDetail } from "../../features/wishlist/useWishlist";
import type { WishlistEntryDetail } from "../../features/wishlist/wishlistApi";
import { useStoreFilters } from "../../features/stores/useStoreFilters";
import { useStoreItemFacets } from "../../features/stores/useStoreItemFacets";
import { useStoreItems } from "../../features/stores/useStoreItems";
import { useInfiniteScrollSentinel } from "../../hooks/useInfiniteScrollSentinel";
import StoreItemFiltersComponent from "../../features/stores/StoreItemFilters";
import StoreProfileEditor from "../../features/stores/StoreProfileEditor";
import { useStoreProfileData } from "../../features/stores/useStoreProfileData";

const StoreDetailPage = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const { store, status } = useStoreDetail(id!);
  const profileData = useStoreProfileData(id!);

  const { activeTab, selectTab } = useHashTab(["items", "profile"], "items");

  const {
    filters,
    setSearch,
    setAvailable,
    setSubscriberOnly,
    setFeatured,
    setCategories,
    setCostMin,
    setCostMax,
    setSort,
    setSortDir,
    resetAll,
    hasActiveFilters,
  } = useStoreFilters();

  const filterOptions = useStoreItemFacets(id!);
  const {
    items,
    status: itemsStatus,
    isRefreshing: itemsRefreshing,
    hasMore,
    loadMore,
    reload: reloadItems,
  } = useStoreItems(id!, filters);
  const sentinelRef = useInfiniteScrollSentinel(loadMore, hasMore);

  const [searchOpen, setSearchOpen] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState<"add" | "edit">("add");
  const [selectedItem, setSelectedItem] = useState<StoreItemDetail | null>(
    null,
  );
  const [editEntry, setEditEntry] = useState<WishlistEntryDetail | null>(null);

  const loadEntry = useWishlistEntryDetail();
  const { showToast } = useToast();

  const handleAddToWishlist = (item: StoreItemDetail) => {
    setSelectedItem(item);
    setEditEntry(null);
    setModalMode("add");
    setModalOpen(true);
  };

  const handleEditWishlist = async (item: StoreItemDetail) => {
    if (!item.wishlistEntryId) return;
    const outcome = await loadEntry.execute(item.wishlistEntryId);
    if (!outcome.ok) {
      showToast({
        kicker: t("wishlist.toast.errorKicker"),
        msg: outcome.message,
        tone: "error",
      });
      return;
    }
    setSelectedItem(item);
    setEditEntry(outcome.entry);
    setModalMode("edit");
    setModalOpen(true);
  };

  const handleModalSaved = () => {
    setModalOpen(false);
    setSelectedItem(null);
    setEditEntry(null);
    // Refetch the grid rather than reloading the document: a wishlist change alters one card's
    // state, and a full reload also discards the active filters and the scroll position.
    reloadItems();
  };

  if (status === "loading") {
    return <p className="t-muted">{t("stores.detailLoading")}</p>;
  }

  if (status === "not_found") {
    return <p style={{ color: "var(--bad)" }}>{t("stores.detailNotFound")}</p>;
  }

  if (status === "error" || !store) {
    return <p style={{ color: "var(--bad)" }}>{t("stores.detailLoadError")}</p>;
  }

  // The profile's `unresolved` array is the authoritative "needs a value" list. `store`'s own
  // missingLabels is kept as a second signal: it is computed by GET /api/stores/{id}, so the dot
  // can still light up if the profile fetch failed.
  const unresolvedCount = profileData.profile?.unresolved.length ?? 0;
  const needsAttention =
    profileData.status !== "loading" &&
    (unresolvedCount > 0 || store.missingLabels.length > 0);

  return (
    <div className="page">
      <PageHead
        title={store.displayName}
        description={`${store.itemCount} items · ch ${store.channelId}`}
        right={
          <>
            <button
              className="btn"
              aria-pressed={searchOpen}
              onClick={() => setSearchOpen((v) => !v)}
            >
              <Search size={13} /> {t("common.search")}
            </button>
            <Pill tone={store.isActive ? "ok" : "warn"}>
              {store.isActive ? t("stores.active") : t("stores.inactive")}
            </Pill>
            <button
              className="btn btn-ghost"
              onClick={() => navigate("/stores")}
            >
              <ArrowLeft size={13} /> {t("stores.backToStores")}
            </button>
          </>
        }
      />

      <Tabs
        tabs={[
          { id: "items", label: t("stores.tabs.items") },
          {
            id: "profile",
            label: t("stores.tabs.profile"),
            indicator: needsAttention ? (
              <NotificationDot label={t("stores.tabs.attention")} />
            ) : undefined,
          },
        ]}
        activeId={activeTab}
        onSelect={selectTab}
      />

      {activeTab === "items" && (
        <>
          {searchOpen && (
            <StoreItemFiltersComponent
              filters={filters}
              options={filterOptions}
              onSearch={setSearch}
              onAvailable={setAvailable}
              onSubscriberOnly={setSubscriberOnly}
              onFeatured={setFeatured}
              onCategories={setCategories}
              onCostMin={setCostMin}
              onCostMax={setCostMax}
              onSort={setSort}
              onSortDir={setSortDir}
              onResetAll={resetAll}
              hasActiveFilters={hasActiveFilters}
            />
          )}

          {/* The store's own count answers "does this store hold anything at all"; the page's
              total answers "does anything match right now". They are different messages, which is
              why the envelope still carries the unfiltered count. */}
          {store.itemCount === 0 ? (
            <EmptyState
              icon={PackageOpen}
              tone="idle"
              title={t("stores.noItems.title")}
              body={t("stores.noItems.body")}
            />
          ) : itemsStatus === "error" ? (
            <p style={{ color: "var(--bad)" }}>{t("stores.detailLoadError")}</p>
          ) : items.length === 0 && itemsStatus === "loading" ? (
            <SkeletonGrid label={t("stores.detailLoading")} />
          ) : items.length === 0 ? (
            <EmptyState
              icon={SearchX}
              tone="filtered"
              title={t("stores.itemsFiltered.title")}
              body={t("stores.itemsFiltered.body")}
              action={
                hasActiveFilters && (
                  <button className="btn" onClick={resetAll}>
                    {t("common.clearFilters")}
                  </button>
                )
              }
            />
          ) : (
            <>
              {/* The rows deliberately stay put while the server re-queries, so without this the
                  only feedback for a filter change is the rows eventually changing. `aria-busy`
                  carries it to assistive tech; the fade carries it to everyone else. */}
              <div
                className="grid grid-3 stagger"
                data-testid="store-item-grid"
                aria-busy={itemsRefreshing}
                style={{
                  opacity: itemsRefreshing ? 0.6 : 1,
                  transition: "opacity 120ms ease",
                }}
              >
                {items.map((item) => (
                  <ItemCard
                    key={item.id}
                    item={item}
                    wishlisted={!!item.wishlistEntryId}
                    onWishlist={() =>
                      item.wishlistEntryId
                        ? handleEditWishlist(item)
                        : handleAddToWishlist(item)
                    }
                  />
                ))}
              </div>

              {/* A real button, not a bare sentinel: the observer only presses it early. That is
                  what keeps loading reachable by keyboard and retryable after a failed fetch. */}
              {hasMore && (
                <div ref={sentinelRef} className="load-more">
                  <button className="btn" onClick={loadMore}>
                    {t("stores.loadMore")}
                  </button>
                </div>
              )}
            </>
          )}
        </>
      )}

      {activeTab === "profile" && (
        <>
          {profileData.status === "loading" && (
            <p className="t-muted">{t("stores.profile.loading")}</p>
          )}
          {profileData.profile && (
            <StoreProfileEditor
              storeId={store.id}
              profile={profileData.profile}
              onSaved={profileData.reload}
            />
          )}
          {profileData.profileFailed && (
            <p style={{ color: "var(--bad)" }}>
              {t("stores.profile.loadError")}
            </p>
          )}
        </>
      )}

      {selectedItem && (
        <WishlistEntryModal
          open={modalOpen}
          onClose={() => {
            setModalOpen(false);
            setSelectedItem(null);
            setEditEntry(null);
          }}
          onSaved={handleModalSaved}
          mode={modalMode}
          storeItemId={selectedItem.id}
          storeId={store.id}
          itemName={selectedItem.name}
          itemCost={selectedItem.cost}
          allowMessages={selectedItem.allowMessages}
          existingEntry={editEntry}
          isBlocked={editEntry?.isBlocked}
          missingLabels={editEntry?.missingLabels}
        />
      )}
    </div>
  );
};

export default StoreDetailPage;
