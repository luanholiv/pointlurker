import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Search, SearchX } from "lucide-react";
import { EmptyState, PageHead } from "../../components/ui";
import { useWishlistList } from "../../features/wishlist/useWishlist";
import WishlistGroupedList from "../../features/wishlist/WishlistGroupedList";

const WishlistPage = () => {
  const [q, setQ] = useState("");
  const [searchOpen, setSearchOpen] = useState(false);
  const { data, status, reload } = useWishlistList({ search: q });
  const { t } = useTranslation();

  // An empty list means two different things, and the wishlist's own empty message tells the
  // reader to go add items — wrong advice when they have simply mistyped a name.
  const searchFoundNothing = Boolean(q) && data?.items.length === 0;

  return (
    <div className="page">
      <PageHead
        title={t("wishlist.title")}
        description={t("wishlist.description")}
        right={
          <button
            className="btn"
            aria-pressed={searchOpen}
            onClick={() => setSearchOpen((v) => !v)}
          >
            <Search size={13} /> {t("common.search")}
          </button>
        }
      />

      {searchOpen && (
        <div className="panel" style={{ marginBottom: 18 }}>
          <div className="search">
            <Search size={15} />
            <input
              className="input"
              placeholder={t("wishlist.searchPlaceholder")}
              aria-label={t("wishlist.searchAriaLabel")}
              value={q}
              onChange={(e) => setQ(e.target.value)}
              autoFocus
            />
          </div>
        </div>
      )}

      {status === "loading" && (
        <p className="t-muted">{t("wishlist.loading")}</p>
      )}

      {status === "error" && (
        <p style={{ color: "var(--bad)" }}>{t("wishlist.loadError")}</p>
      )}

      {status === "ready" && searchFoundNothing && (
        <EmptyState
          icon={SearchX}
          tone="filtered"
          title={t("wishlist.searchEmpty.title")}
          body={t("wishlist.searchEmpty.body", { query: q })}
          action={
            <button className="btn" onClick={() => setQ("")}>
              {t("wishlist.searchEmpty.cta")}
            </button>
          }
        />
      )}

      {status === "ready" && data && !searchFoundNothing && (
        <WishlistGroupedList data={data} onChanged={reload} />
      )}
    </div>
  );
};

export default WishlistPage;
