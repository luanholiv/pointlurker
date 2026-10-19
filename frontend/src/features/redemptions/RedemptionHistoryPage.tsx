import { useState } from "react";
import { Heart, History, Search, SearchX } from "lucide-react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import {
  EmptyState,
  PageHead,
  Chip,
  Pill,
  type PillTone,
} from "../../components/ui";
import { useRedemptions } from "./useRedemptions";
import ProviderBadge from "../stores/ProviderBadge";
import type {
  RedemptionItem,
  RedemptionSortField,
  RedemptionStatus,
} from "./redemptionsApi";
import { formatDate } from "../../i18n/formatters";
import { tError } from "../../i18n/errorCatalog";

type StatusFilter = RedemptionStatus | "";

/** The server orders the whole history; these are the field names it accepts. */
type SortKey = RedemptionSortField;

// Keyed by RedemptionStatus rather than by string, so a member added to the union without a tone
// here is a type error. Typed loosely, a status the map did not know simply fell through to the
// fallback below and was painted as if it were pending.
const STATUS_TONE: Record<RedemptionStatus, PillTone> = {
  Pending: "warn",
  Submitted: "accent",
  Completed: "ok",
  SubmissionFailed: "bad",
  CompletionTimedOut: "bad",
  Rejected: "bad",
};

const RedemptionHistoryPage = () => {
  const { t, i18n } = useTranslation();
  const [filterOpen, setFilterOpen] = useState(false);
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [sortKey, setSortKey] = useState<SortKey>("attemptedAt");
  const [sortAsc, setSortAsc] = useState(false);

  const isFiltered = Boolean(statusFilter || from || to);
  const clearFilters = () => {
    setStatusFilter("");
    setFrom("");
    setTo("");
    setPage(1);
  };

  const pageSize = 20;

  const { data, status, isRefreshing, reload } = useRedemptions({
    status: statusFilter || null,
    from: from || null,
    to: to || null,
    sort: sortKey,
    dir: sortAsc ? "asc" : "desc",
    page,
    pageSize,
  });

  const STATUS_CHIPS: Array<{ value: StatusFilter; label: string }> = [
    { value: "", label: t("redemption.filter.allChip") },
    { value: "Completed", label: t("redemption.filter.successChip") },
    { value: "SubmissionFailed", label: t("redemption.filter.failedChip") },
    { value: "Pending", label: t("redemption.filter.pendingChip") },
    // Submitted has its own chip: it is the state a redemption gets stuck in when its completion
    // cannot be read, and without a chip those rows could not be listed at all.
    { value: "Submitted", label: t("redemption.filter.submittedChip") },
  ];

  const COLUMNS: Array<{ key: SortKey; label: string }> = [
    { key: "status", label: t("redemption.columns.status") },
    { key: "storeItemName", label: t("redemption.columns.item") },
    { key: "storeName", label: t("redemption.columns.store") },
    { key: "attemptedAt", label: t("redemption.columns.attempted") },
    { key: "submittedAt", label: t("redemption.columns.submitted") },
    { key: "settledAt", label: t("redemption.columns.completed") },
  ];

  // Sorting is a request, not a re-arrangement of the loaded page — which is what the previous
  // implementation did, sorting twenty rows and presenting the result as a sorted table. Changing
  // the order returns to the first page, because "first" now means first overall.
  const handleSort = (key: SortKey) => {
    if (sortKey === key) setSortAsc((a) => !a);
    else {
      setSortKey(key);
      setSortAsc(true);
    }
    setPage(1);
  };

  const rows = data?.items ?? [];

  const totalPages = data
    ? Math.max(1, Math.ceil(data.totalCount / pageSize))
    : 1;

  const fmtDate = (iso: string | null) => {
    if (!iso) return "—";
    return formatDate(iso, i18n.language, {
      month: "short",
      day: "numeric",
      year: "numeric",
      hour: "numeric",
      minute: "2-digit",
    });
  };

  // The server sends the error code itself, so there is nothing to translate here — tError resolves
  // it against the catalog and falls back to the code when a build sends one this catalog predates.
  //
  // With no failure code, a non-zero poll-failure count is the only thing left to say: the
  // redemption was submitted and its outcome cannot be read. That reads as an ordinary pending row
  // otherwise, which is exactly how a broken completion check stayed invisible.
  const renderFailure = (r: RedemptionItem) => {
    if (r.failureCode) return tError(r.failureCode, {});
    if (r.pollFailures > 0)
      return t("redemption.statusCheckFailing", { count: r.pollFailures });
    return "—";
  };

  return (
    <div className="page">
      <PageHead
        title={t("redemption.title")}
        description={t("redemption.description")}
        right={
          <button
            className="btn"
            aria-pressed={filterOpen}
            onClick={() => setFilterOpen((v) => !v)}
          >
            <Search size={13} /> {t("common.search")}
          </button>
        }
      />

      {filterOpen && (
        <div className="panel" style={{ marginBottom: 18 }}>
          <div className="vstack" style={{ gap: 14 }}>
            <div className="hstack" style={{ flexWrap: "wrap", gap: 6 }}>
              {STATUS_CHIPS.map((chip) => (
                <Chip
                  key={chip.value}
                  pressed={statusFilter === chip.value}
                  onToggle={() => {
                    setStatusFilter(chip.value);
                    setPage(1);
                  }}
                >
                  {chip.label}
                </Chip>
              ))}
            </div>
            <div className="hstack" style={{ flexWrap: "wrap", gap: 12 }}>
              <div className="hstack" style={{ gap: 6 }}>
                <label htmlFor="redemption-from" className="t-meta t-muted">
                  {t("redemption.filter.from")}
                </label>
                <input
                  id="redemption-from"
                  type="datetime-local"
                  aria-label={t("redemption.filter.fromAriaLabel")}
                  value={from}
                  onChange={(e) => {
                    setFrom(e.target.value);
                    setPage(1);
                  }}
                  className="input"
                  style={{ padding: "4px 8px" }}
                />
              </div>
              <div className="hstack" style={{ gap: 6 }}>
                <label htmlFor="redemption-to" className="t-meta t-muted">
                  {t("redemption.filter.to")}
                </label>
                <input
                  id="redemption-to"
                  type="datetime-local"
                  aria-label={t("redemption.filter.toAriaLabel")}
                  value={to}
                  onChange={(e) => {
                    setTo(e.target.value);
                    setPage(1);
                  }}
                  className="input"
                  style={{ padding: "4px 8px" }}
                />
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="panel">
        <div className="panel-head">
          <div className="t-eyebrow">
            {data ? t("redemption.results", { count: data.totalCount }) : "—"}
          </div>
          <div className="hstack">
            {statusFilter && (
              <Pill tone={STATUS_TONE[statusFilter]}>
                {t(`redemption.status.${statusFilter}`, {
                  defaultValue: statusFilter,
                })}
              </Pill>
            )}
          </div>
        </div>

        {status === "loading" && (
          <p className="t-muted">{t("redemption.loading")}</p>
        )}

        {status === "error" && (
          <div className="hstack" style={{ gap: 10 }}>
            <p className="t-small t-bad">{t("redemption.loadError")}</p>
            <button className="btn btn-sm" onClick={reload}>
              {t("redemption.retry")}
            </button>
          </div>
        )}

        {status === "ready" && rows.length === 0 && !isFiltered && (
          <EmptyState
            icon={History}
            title={t("redemption.empty.title")}
            body={t("redemption.empty.body")}
            action={
              <Link className="btn" to="/wishlist">
                <Heart size={14} aria-hidden={true} focusable="false" />
                {t("redemption.empty.cta")}
              </Link>
            }
          />
        )}

        {status === "ready" && rows.length === 0 && isFiltered && (
          <EmptyState
            icon={SearchX}
            tone="filtered"
            title={t("redemption.emptyFiltered.title")}
            body={t("redemption.emptyFiltered.body")}
            action={
              <button className="btn" onClick={clearFilters}>
                {t("common.clearFilters")}
              </button>
            }
          />
        )}

        {status === "ready" && rows.length > 0 && (
          <>
            <div style={{ overflowX: "auto" }}>
              {/* Sorting is a request now, so the click does not reorder anything on its own.
                  Without a busy marker a slow query is indistinguishable from a dead header. */}
              <table
                aria-busy={isRefreshing}
                className="t-small"
                style={{
                  width: "100%",
                  borderCollapse: "collapse",
                  opacity: isRefreshing ? 0.6 : 1,
                  transition: "opacity 120ms ease",
                }}
              >
                <thead>
                  <tr style={{ borderBottom: "1px dashed var(--line-soft)" }}>
                    {COLUMNS.map((col) => (
                      <th
                        key={col.key}
                        onClick={() => handleSort(col.key)}
                        className="t-eyebrow"
                        style={{
                          padding: "8px 12px 8px 0",
                          textAlign: "left",
                          cursor: "pointer",
                          userSelect: "none",
                        }}
                      >
                        {col.label}
                        {sortKey === col.key && (
                          <span style={{ marginLeft: 4 }}>
                            {sortAsc ? "▲" : "▼"}
                          </span>
                        )}
                      </th>
                    ))}
                    <th className="t-eyebrow" style={{ padding: "8px 0 8px" }}>
                      {t("redemption.columns.reason")}
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr
                      key={r.id}
                      style={{ borderBottom: "1px solid var(--line-soft)" }}
                    >
                      <td style={{ padding: "10px 12px 10px 0" }}>
                        {/* The fallback stays here and not on the filter pill above: that one
                            reads local state the union already constrains, while this reads a
                            status off the wire, which a newer server can widen. */}
                        <Pill tone={STATUS_TONE[r.status] ?? "warn"}>
                          {t(`redemption.status.${r.status}`, {
                            defaultValue: r.status,
                          })}
                        </Pill>
                      </td>
                      <td
                        style={{
                          padding: "10px 12px 10px 0",
                          color: "var(--ink)",
                        }}
                      >
                        {r.storeItemName}
                      </td>
                      <td
                        style={{
                          padding: "10px 12px 10px 0",
                          color: "var(--ink-2)",
                        }}
                      >
                        <span
                          className="hstack"
                          style={{ gap: 6, alignItems: "center" }}
                        >
                          {r.storeName}
                          {r.provider && (
                            <ProviderBadge provider={r.provider} />
                          )}
                        </span>
                      </td>
                      <td
                        className="t-meta t-muted"
                        style={{ padding: "10px 12px 10px 0" }}
                      >
                        {fmtDate(r.attemptedAt)}
                      </td>
                      <td
                        className="t-meta t-muted"
                        style={{ padding: "10px 12px 10px 0" }}
                      >
                        {fmtDate(r.submittedAt)}
                      </td>
                      <td
                        className="t-meta t-muted"
                        style={{ padding: "10px 12px 10px 0" }}
                      >
                        {fmtDate(r.settledAt)}
                      </td>
                      <td className="t-dim" style={{ padding: "10px 0 10px" }}>
                        {renderFailure(r)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="between t-small t-muted" style={{ marginTop: 12 }}>
              <span>
                {data!.totalCount === 0
                  ? t("redemption.noResultsPagination")
                  : t("redemption.pagination", {
                      from: (page - 1) * pageSize + 1,
                      to: Math.min(page * pageSize, data!.totalCount),
                      total: data!.totalCount,
                    })}
              </span>
              <div className="hstack" style={{ gap: 8 }}>
                <button
                  className="btn btn-sm btn-ghost"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => p - 1)}
                >
                  {t("redemption.previous")}
                </button>
                <span className="t-meta">
                  {page} / {totalPages}
                </span>
                <button
                  className="btn btn-sm btn-ghost"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  {t("redemption.next")}
                </button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
};

export default RedemptionHistoryPage;
