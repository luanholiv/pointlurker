import { Search } from "lucide-react";
import { useTranslation } from "react-i18next";
import Chip from "../../components/ui/Chip";
import type { StoreItemFilters } from "./types";
import { defaultSortFor } from "./useStoreFilters";
import type { StoreFilterOptions } from "./useStoreItemFacets";

type Props = {
  filters: StoreItemFilters;
  options: StoreFilterOptions;
  onSearch: (v: string) => void;
  onAvailable: (v: boolean) => void;
  onSubscriberOnly: (v: boolean) => void;
  onFeatured: (v: boolean) => void;
  onCategories: (v: string[]) => void;
  onCostMin: (v: number | null) => void;
  onCostMax: (v: number | null) => void;
  onSort: (v: StoreItemFilters["sort"]) => void;
  onSortDir: (v: StoreItemFilters["sortDir"]) => void;
  onResetAll: () => void;
  hasActiveFilters: boolean;
};

const StoreItemFiltersComponent = ({
  filters,
  options,
  onSearch,
  onAvailable,
  onSubscriberOnly,
  onFeatured,
  onCategories,
  onCostMin,
  onCostMax,
  onSort,
  onSortDir,
  onResetAll,
  hasActiveFilters,
}: Props) => {
  const { t } = useTranslation();

  // Relevance is offered only while there is a term to rank by, which is also when it is the
  // ordering already in effect. Listing it against an empty search box would name an ordering the
  // server answers by name anyway.
  const SORT_OPTIONS: { value: StoreItemFilters["sort"]; label: string }[] = [
    ...(filters.search
      ? [
          {
            value: "relevance" as const,
            label: t("stores.filters.sortRelevance"),
          },
        ]
      : []),
    { value: "name", label: t("stores.filters.sortName") },
    { value: "cost", label: t("stores.filters.sortCost") },
    { value: "stock", label: t("stores.filters.sortStock") },
    { value: "date", label: t("stores.filters.sortDate") },
  ];

  const toggleCategory = (cat: string) => {
    if (filters.categories.includes(cat)) {
      onCategories(filters.categories.filter((c) => c !== cat));
    } else {
      onCategories([...filters.categories, cat]);
    }
  };

  const activeChips: { label: string; onDismiss: () => void }[] = [];

  if (filters.search) {
    activeChips.push({
      label: `"${filters.search}"`,
      onDismiss: () => onSearch(""),
    });
  }
  if (!filters.available) {
    activeChips.push({
      label: t("stores.filters.showingUnavailable"),
      onDismiss: () => onAvailable(true),
    });
  }
  if (filters.subscriberOnly) {
    activeChips.push({
      label: t("stores.filters.subscriberOnly"),
      onDismiss: () => onSubscriberOnly(false),
    });
  }
  if (filters.featured) {
    activeChips.push({
      label: t("stores.filters.featured"),
      onDismiss: () => onFeatured(false),
    });
  }
  for (const cat of filters.categories) {
    activeChips.push({
      label: cat,
      onDismiss: () =>
        onCategories(filters.categories.filter((c) => c !== cat)),
    });
  }
  if (filters.costMin !== null) {
    activeChips.push({
      label: t("stores.filters.minFilter", { value: filters.costMin }),
      onDismiss: () => onCostMin(null),
    });
  }
  if (filters.costMax !== null) {
    activeChips.push({
      label: t("stores.filters.maxFilter", { value: filters.costMax }),
      onDismiss: () => onCostMax(null),
    });
  }
  // Compared against the ordering this view would use anyway, not against "name": while a term is
  // present that ordering is relevance, and chipping the default would ask the reader to dismiss
  // the state they are already in.
  const defaultSort = defaultSortFor(filters.search);

  if (filters.sort !== defaultSort) {
    const sortLabel =
      SORT_OPTIONS.find((o) => o.value === filters.sort)?.label ?? filters.sort;
    activeChips.push({
      label: t("stores.filters.sortFilter", { label: sortLabel }),
      onDismiss: () => onSort(defaultSort),
    });
  }
  if (filters.sortDir !== "asc") {
    activeChips.push({
      label: t("stores.filters.sortDesc"),
      onDismiss: () => onSortDir("asc"),
    });
  }

  return (
    <div className="panel" style={{ marginBottom: 18 }}>
      <div className="vstack" style={{ gap: 14 }}>
        <div className="search">
          <Search size={15} />
          <input
            type="search"
            className="input"
            aria-label={t("stores.filters.searchAriaLabel")}
            placeholder={t("stores.filters.searchPlaceholder")}
            value={filters.search}
            onChange={(e) => onSearch(e.target.value)}
            autoFocus
          />
        </div>

        <div className="hstack" style={{ flexWrap: "wrap", gap: 6 }}>
          <Chip
            pressed={filters.available}
            onToggle={() => onAvailable(!filters.available)}
          >
            {t("stores.filters.available")}
          </Chip>
          <Chip
            pressed={filters.subscriberOnly}
            onToggle={() => onSubscriberOnly(!filters.subscriberOnly)}
          >
            {t("stores.filters.subscriberOnly")}
          </Chip>
          <Chip
            pressed={filters.featured}
            onToggle={() => onFeatured(!filters.featured)}
          >
            {t("stores.filters.featured")}
          </Chip>
        </div>

        {options.categories.length > 0 && (
          <div className="vstack" style={{ gap: 6 }}>
            <span className="t-eyebrow">{t("stores.filters.categories")}</span>
            <div
              className="hstack"
              style={{ flexWrap: "wrap", gap: 6 }}
              aria-label={t("stores.filters.categoryAriaLabel")}
            >
              {options.categories.map((cat) => (
                <Chip
                  key={cat}
                  pressed={filters.categories.includes(cat)}
                  onToggle={() => toggleCategory(cat)}
                >
                  {cat}
                </Chip>
              ))}
            </div>
          </div>
        )}

        <div className="hstack" style={{ flexWrap: "wrap", gap: 12 }}>
          <div className="hstack" style={{ gap: 6 }}>
            <label htmlFor="cost-min" className="t-meta t-muted">
              {t("stores.filters.minPts")}
            </label>
            <input
              id="cost-min"
              type="number"
              className="input"
              aria-label={t("stores.filters.minCostAriaLabel")}
              style={{ width: 88 }}
              min={options.costBounds.min}
              max={options.costBounds.max}
              value={filters.costMin ?? ""}
              onChange={(e) =>
                onCostMin(e.target.value === "" ? null : Number(e.target.value))
              }
              placeholder={String(options.costBounds.min)}
            />
          </div>
          <div className="hstack" style={{ gap: 6 }}>
            <label htmlFor="cost-max" className="t-meta t-muted">
              {t("stores.filters.maxPts")}
            </label>
            <input
              id="cost-max"
              type="number"
              className="input"
              aria-label={t("stores.filters.maxCostAriaLabel")}
              style={{ width: 88 }}
              min={options.costBounds.min}
              max={options.costBounds.max}
              value={filters.costMax ?? ""}
              onChange={(e) =>
                onCostMax(e.target.value === "" ? null : Number(e.target.value))
              }
              placeholder={String(options.costBounds.max)}
            />
          </div>
        </div>

        <div className="hstack" style={{ flexWrap: "wrap", gap: 8 }}>
          <span className="t-meta t-muted">{t("stores.filters.sortBy")}</span>
          <select
            id="sort-field"
            className="select"
            aria-label={t("stores.filters.sortFieldAriaLabel")}
            value={filters.sort}
            onChange={(e) => onSort(e.target.value as StoreItemFilters["sort"])}
          >
            {SORT_OPTIONS.map((opt) => (
              <option key={opt.value} value={opt.value}>
                {opt.label}
              </option>
            ))}
          </select>
          <select
            className="select"
            aria-label={t("stores.filters.sortDirAriaLabel")}
            value={filters.sortDir}
            onChange={(e) =>
              onSortDir(e.target.value as StoreItemFilters["sortDir"])
            }
          >
            <option value="asc">{t("stores.filters.sortAsc")}</option>
            <option value="desc">{t("stores.filters.sortDesc")}</option>
          </select>
        </div>

        {activeChips.length > 0 && (
          <div
            className="hstack"
            style={{ flexWrap: "wrap", gap: 6 }}
            aria-label={t("stores.filters.activeFiltersAriaLabel")}
          >
            {activeChips.map((chip) => (
              <span
                key={chip.label}
                className="t-small"
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 3,
                  background: "var(--surface)",
                  border: "1px solid var(--accent)",
                  borderRadius: "var(--r-1)",
                  padding: "2px 8px 2px 10px",
                  color: "var(--accent)",
                  fontWeight: 500,
                }}
              >
                {chip.label}
                <button
                  type="button"
                  aria-label={t("stores.filters.removeFilterAriaLabel", {
                    label: chip.label,
                  })}
                  onClick={chip.onDismiss}
                  style={{
                    background: "none",
                    border: "none",
                    cursor: "pointer",
                    padding: "0 2px",
                    lineHeight: 1,
                    color: "var(--ink-3)",
                  }}
                >
                  ×
                </button>
              </span>
            ))}
            {hasActiveFilters && (
              <button
                type="button"
                className="btn btn-sm btn-ghost"
                onClick={onResetAll}
              >
                {t("stores.filters.resetAll")}
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
};

export default StoreItemFiltersComponent;
