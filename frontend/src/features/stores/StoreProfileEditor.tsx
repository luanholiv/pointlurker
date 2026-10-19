import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Tags } from "lucide-react";
import EmptyState from "../../components/ui/EmptyState";
import {
  deleteStoreProfileAlias,
  deleteStoreProfileValue,
  upsertStoreProfileAlias,
  upsertStoreProfileValues,
  type InputResolutionSource,
  type StoreInputProfile,
  type StoreProfileRequester,
  type StoreProfileResolvedInput,
  type StoreProfileUnresolvedInput,
  type StoreProfileValueUpsert,
} from "./storesApi";
import { formatDate } from "../../i18n/formatters";
import FieldError from "../../components/ui/FieldError";
import { fieldErrorProps } from "../../components/ui/fieldErrorProps";
import Pill, { type PillTone } from "../../components/ui/Pill";
import {
  splitFieldErrors,
  type FieldAnchor,
  type FieldErrorOptions,
} from "../../i18n/fieldErrors";
import {
  fieldIndex,
  type FieldLabelMap,
  type TranslateFn,
} from "../../i18n/fieldLabels";
import { tApiError } from "../../i18n/errorCatalog";
import { ApiError } from "../../lib/apiClient";

type StoreProfileEditorProps = {
  storeId: string;
  profile: StoreInputProfile;
  onSaved: () => Promise<void> | void;
};

type Mode = "value" | "alias";

/**
 * One request of a save. A save fans out to several, each reporting its own fields, so each
 * carries the anchor that maps those fields back to a row on screen.
 */
type SaveOp = {
  label: string;
  run: Promise<unknown>;
  anchor: FieldAnchor;
  override: FieldErrorOptions["override"];
  /**
   * Error slots for the inputs this request carried, used when a rejection explains itself but
   * names no field. Kept apart from `clears` because these say where a *message* goes, and an
   * alias request's message belongs against its canonical picker rather than a value input.
   */
  rows: string[];
  /** Row keys whose typed value this request stored, emptied once it is accepted. */
  clears: string[];
};

/**
 * Names for fields no row can be found for — an index past the end of what was submitted, or a
 * field a future rule adds. The rows themselves are labelled with strings from the provider,
 * so they come from the data rather than from here.
 */
const PROFILE_LABELS: FieldLabelMap = {
  rawLabel: "stores.profile.rawLabelField",
  labelCanonical: "stores.profile.canonicalField",
  "values[].labelCanonical": "stores.profile.canonicalField",
  "values[].value": "stores.profile.valuePlaceholder",
};

/** The canonical picker is a second input on the same row, so it needs a key of its own. */
const canonicalKey = (rawLabel: string) => `${rawLabel}::canonical`;

/**
 * Codes naming a failure of the store itself rather than of anything on this panel. The store row
 * is gone, so no input here can act on the message and repeating it under each one is noise.
 *
 * This is the only backend code the editor holds by name; every other outcome is routed by shape.
 */
const STORE_SCOPED = new Set(["stores.not_found"]);

/**
 * Whether a rejection that named no field can still be blamed on the inputs its request carried.
 *
 * Only a problem document says anything about *this* request's content. A dropped connection, an
 * expired session or a bodyless 500 arrives as a plain `Error` and says nothing about any one row,
 * so it — like a vanished store — is reported once at panel level instead.
 */
const isRowAttributable = (reason: unknown) =>
  reason instanceof ApiError && !STORE_SCOPED.has(reason.code);

/**
 * One line per distinct reason, naming every operation it accounted for. A save that fails
 * wholesale rejects each of its requests identically, and several copies of one sentence is a
 * worse report than one sentence listing the labels it covered.
 */
const groupByReason = (
  entries: { label: string; message: string }[],
  t: TranslateFn,
): string[] => {
  const labels = new Map<string, string[]>();
  for (const { label, message } of entries) {
    const seen = labels.get(message);
    if (seen) {
      seen.push(label);
    } else {
      labels.set(message, [label]);
    }
  }
  return [...labels].map(([message, grouped]) =>
    t("validation.fieldMessage", { label: grouped.join(", "), message }),
  );
};

/**
 * A row is a *slot* — something the store needs a value for — not a record of which endpoint
 * supplied it. Unresolved slots come first because they are the actionable ones; both arrays
 * arrive pre-sorted from the server and are rendered in that order verbatim.
 */
type Row =
  | { kind: "unresolved"; key: string; input: StoreProfileUnresolvedInput }
  | { kind: "resolved"; key: string; input: StoreProfileResolvedInput };

const buildRows = (profile: StoreInputProfile): Row[] => [
  ...profile.unresolved.map((input): Row => ({
    kind: "unresolved",
    key: input.rawLabel,
    input,
  })),
  ...profile.resolved.map((input): Row => ({
    kind: "resolved",
    key: input.labelCanonical,
    input,
  })),
];

const StoreProfileEditor = ({
  storeId,
  profile,
  onSaved,
}: StoreProfileEditorProps) => {
  const { t, i18n } = useTranslation();
  const rows = buildRows(profile);
  const { canonicalLabels } = profile;

  // Keyed by row key: the raw label for unresolved slots, the canonical for resolved ones.
  const [values, setValues] = useState<Record<string, string>>({});
  const [modes, setModes] = useState<Record<string, Mode>>({});
  const [canonicals, setCanonicals] = useState<Record<string, string>>({});
  const [confirming, setConfirming] = useState<string | null>(null);
  // Only whether a save is in flight: a failure is told by the messages below, not by a status.
  const [status, setStatus] = useState<"idle" | "saving">("idle");
  // Merged across every failed operation of one save, so two rows that broke are both reported.
  const [fieldMessages, setFieldMessages] = useState<Record<string, string[]>>(
    {},
  );
  const [formMessages, setFormMessages] = useState<string[]>([]);

  const modeOf = (key: string): Mode => modes[key] ?? "value";

  const setMode = (row: StoreProfileUnresolvedInput, mode: Mode) => {
    setModes((prev) => ({ ...prev, [row.rawLabel]: mode }));
    // Prefill the picker with the server's best candidate. This is a convenience on an
    // explicit user action, not a confidence threshold — the frontend holds no such policy.
    if (mode === "alias" && row.bestCanonical) {
      setCanonicals((prev) => ({
        ...prev,
        [row.rawLabel]: prev[row.rawLabel] ?? row.bestCanonical!,
      }));
    }
  };

  const valueUpserts = (): StoreProfileValueUpsert[] => {
    const out: StoreProfileValueUpsert[] = [];
    for (const row of rows) {
      if (row.kind === "unresolved" && modeOf(row.key) !== "value") continue;
      const entered = values[row.key];
      if (entered && entered.trim().length > 0) {
        out.push({ labelCanonical: row.key, value: entered });
      }
    }
    return out;
  };

  const aliasOps = (): { rawLabel: string; canonical: string }[] => {
    const out: { rawLabel: string; canonical: string }[] = [];
    for (const row of rows) {
      if (row.kind !== "unresolved" || modeOf(row.key) !== "alias") continue;
      const canonical = canonicals[row.key];
      if (canonical && canonical.trim().length > 0) {
        out.push({ rawLabel: row.key, canonical });
      }
    }
    return out;
  };

  const hasActionable = valueUpserts().length > 0 || aliasOps().length > 0;

  const handleSave = async () => {
    const upserts = valueUpserts();
    const aliases = aliasOps();
    if (upserts.length === 0 && aliases.length === 0) return;

    setStatus("saving");
    setFieldMessages({});
    setFormMessages([]);

    const ops: SaveOp[] = [];
    if (upserts.length > 0) {
      const submitted = upserts.map((u) => u.labelCanonical);
      // The values endpoint takes every row at once, so a rule names the element it broke by its
      // position in *this* array — which is the only place that index can be resolved to a row.
      const rowAt = (field: string) => {
        const index = fieldIndex(field);
        return index === null ? null : (upserts[index]?.labelCanonical ?? null);
      };
      ops.push({
        label: submitted.join(", "),
        run: upsertStoreProfileValues(storeId, upserts),
        anchor: rowAt,
        override: rowAt,
        rows: submitted,
        clears: submitted,
      });
    }
    for (const alias of aliases) {
      ops.push({
        label: alias.rawLabel,
        run: upsertStoreProfileAlias(storeId, alias.rawLabel, alias.canonical),
        // One request per alias, so both of its fields belong to this row: the canonical picker
        // has its own input, and `rawLabel` is not editable and falls back to the value input.
        anchor: (field) =>
          field === "labelCanonical"
            ? canonicalKey(alias.rawLabel)
            : alias.rawLabel,
        override: () => alias.rawLabel,
        // The picker is the input the user chose with, so an unfielded reason belongs under it.
        rows: [canonicalKey(alias.rawLabel)],
        // An alias stores no typed value, so this request leaves no input to empty.
        clears: [],
      });
    }

    const results = await Promise.allSettled(ops.map((o) => o.run));

    const byField: Record<string, string[]> = {};
    const loose: string[] = [];
    const panel: { label: string; message: string }[] = [];

    results.forEach((result, index) => {
      if (result.status !== "rejected") return;
      const op = ops[index];
      const split = splitFieldErrors(result.reason, {
        anchor: op.anchor,
        map: PROFILE_LABELS,
        t,
        override: op.override,
      });
      if (split === null) {
        // No per-field entries, but the reason is still the user's to read.
        const message = tApiError(result.reason, "stores.profile.saveFailed");
        if (isRowAttributable(result.reason)) {
          for (const row of op.rows) {
            (byField[row] ??= []).push(message);
          }
        } else {
          panel.push({ label: op.label, message });
        }
        return;
      }
      for (const [key, messages] of Object.entries(split.byField)) {
        (byField[key] ??= []).push(...messages);
      }
      loose.push(...split.unattached);
    });

    // Emptied per request rather than wholesale: a fan-out can half-succeed, and re-typing a
    // value the server already took is work the user should not be given.
    const stored = results.flatMap((result, index) =>
      result.status === "fulfilled" ? ops[index].clears : [],
    );
    if (stored.length > 0) {
      setValues((prev) => {
        const next = { ...prev };
        for (const key of stored) {
          delete next[key];
        }
        return next;
      });
    }

    setStatus("idle");

    const failed =
      panel.length > 0 || loose.length > 0 || Object.keys(byField).length > 0;

    if (failed) {
      setFieldMessages(byField);
      setFormMessages([...groupByReason(panel, t), ...loose]);
      // Reload anyway when part of the fan-out was accepted, or the rows it stored would keep
      // rendering as unset while the server holds their values.
      if (results.some((result) => result.status === "fulfilled")) {
        await onSaved();
      }
      return;
    }

    await onSaved();
  };

  const runDestructive = async (
    row: string,
    label: string,
    op: Promise<unknown>,
  ) => {
    // Messages from an earlier save belong to inputs this reload may replace, so they go first.
    setFieldMessages({});
    setFormMessages([]);
    try {
      await op;
    } catch (err) {
      const message = tApiError(err, "stores.profile.deleteFailed");
      if (isRowAttributable(err)) {
        setFieldMessages({ [row]: [message] });
      } else {
        setFormMessages([t("validation.fieldMessage", { label, message })]);
      }
      return;
    }
    setConfirming(null);
    await onSaved();
  };

  const handleDeleteValue = (labelCanonical: string) =>
    runDestructive(
      labelCanonical,
      labelCanonical,
      deleteStoreProfileValue(storeId, labelCanonical),
    );

  /**
   * The chip names the requester's raw label, which is not a row key — the row holding it is the
   * resolved one the alias points at. The caller binds that; the label travels separately because
   * it is what the message names.
   */
  const handleDetachAlias = (row: string, rawLabel: string) =>
    runDestructive(row, rawLabel, deleteStoreProfileAlias(storeId, rawLabel));

  return (
    <div
      id="profile"
      className="panel"
      data-testid="store-profile-editor"
      style={{ padding: 14 }}
    >
      <div className="panel-head">
        <div className="t-heading">{t("stores.profile.title")}</div>
      </div>

      <div className="vstack" style={{ gap: 10 }}>
        {rows.length === 0 && (
          <EmptyState
            icon={Tags}
            tone="idle"
            title={t("stores.profile.empty.title")}
            body={t("stores.profile.empty.body")}
          />
        )}
        {rows.map((row, rowIndex) =>
          row.kind === "unresolved" ? (
            <UnresolvedRow
              key={`u:${row.key}`}
              rowIndex={rowIndex}
              input={row.input}
              mode={modeOf(row.key)}
              canonical={canonicals[row.key] ?? ""}
              value={values[row.key] ?? ""}
              canonicalLabels={canonicalLabels}
              valueErrors={fieldMessages[row.key]}
              canonicalErrors={fieldMessages[canonicalKey(row.key)]}
              onMode={(mode) => setMode(row.input, mode)}
              onCanonical={(canonical) =>
                setCanonicals((prev) => ({ ...prev, [row.key]: canonical }))
              }
              onValue={(value) =>
                setValues((prev) => ({ ...prev, [row.key]: value }))
              }
            />
          ) : (
            <ResolvedRow
              key={`r:${row.key}`}
              rowIndex={rowIndex}
              input={row.input}
              language={i18n.language}
              value={values[row.key] ?? ""}
              valueErrors={fieldMessages[row.key]}
              confirming={confirming === row.key}
              onValue={(value) =>
                setValues((prev) => ({ ...prev, [row.key]: value }))
              }
              onAskDelete={() => setConfirming(row.key)}
              onCancelDelete={() => setConfirming(null)}
              onConfirmDelete={() => handleDeleteValue(row.key)}
              onDetach={(rawLabel) => handleDetachAlias(row.key, rawLabel)}
            />
          ),
        )}
      </div>

      {formMessages.length > 0 && (
        <div role="alert">
          {formMessages.map((message) => (
            <p key={message} className="t-small t-bad">
              {message}
            </p>
          ))}
        </div>
      )}

      <div className="hstack" style={{ marginTop: 12, gap: 8 }}>
        <button
          className="btn btn-accent"
          type="button"
          disabled={!hasActionable || status === "saving"}
          onClick={handleSave}
        >
          {status === "saving" ? t("common.saving") : t("stores.profile.save")}
        </button>
      </div>
    </div>
  );
};

type RowShellProps = {
  label: string;
  tone: PillTone;
  pillLabel: string;
  meta?: React.ReactNode;
  subLines?: React.ReactNode;
  children: React.ReactNode;
};

/**
 * The single card template. Every row — resolved, unresolved, unrequested — gets the same
 * shell, header grid and sub-line typography. Only `children` differs.
 */
const RowShell = ({
  label,
  tone,
  pillLabel,
  meta,
  subLines,
  children,
}: RowShellProps) => (
  <div
    className="card"
    data-testid={`profile-row-${label}`}
    style={{ padding: 12 }}
  >
    <div
      className="hstack"
      style={{ justifyContent: "space-between", alignItems: "center", gap: 8 }}
    >
      <strong style={{ minWidth: 0, overflowWrap: "anywhere" }}>{label}</strong>
      <span className="hstack" style={{ gap: 8, flexShrink: 0 }}>
        <Pill tone={tone} testId={`profile-status-${label}`}>
          {pillLabel}
        </Pill>
        {meta && <span className="t-meta t-muted">{meta}</span>}
      </span>
    </div>
    {subLines && (
      <div className="vstack t-small t-muted" style={{ gap: 2, marginTop: 4 }}>
        {subLines}
      </div>
    )}
    {children}
  </div>
);

type UnresolvedRowProps = {
  input: StoreProfileUnresolvedInput;
  mode: Mode;
  canonical: string;
  value: string;
  canonicalLabels: string[];
  /**
   * Position of the row, used only to build ids. The labels come from the provider and contain
   * spaces, and `aria-describedby` is a space-separated list of ids — an id built from one would
   * be read as several, none of which exist.
   */
  rowIndex: number;
  valueErrors?: string[];
  canonicalErrors?: string[];
  onMode: (mode: Mode) => void;
  onCanonical: (canonical: string) => void;
  onValue: (value: string) => void;
};

const UnresolvedRow = ({
  input,
  mode,
  canonical,
  value,
  canonicalLabels,
  rowIndex,
  valueErrors,
  canonicalErrors,
  onMode,
  onCanonical,
  onValue,
}: UnresolvedRowProps) => {
  const { t } = useTranslation();
  const label = input.rawLabel;
  const valueErrorId = `profile-value-error-${rowIndex}`;
  const canonicalErrorId = `profile-canonical-error-${rowIndex}`;
  const noCanonicals = canonicalLabels.length === 0;

  return (
    <RowShell
      label={label}
      tone="warn"
      pillLabel={t("stores.profile.pill.needsValue")}
      subLines={
        <>
          <span data-testid={`profile-meta-${label}`}>
            {t("stores.profile.requestedByItems", { count: input.itemCount })}
            {input.blockedEntryCount > 0 && (
              <>
                {" · "}
                {t("stores.profile.blocksEntries", {
                  count: input.blockedEntryCount,
                })}
              </>
            )}
          </span>
          {input.aliasTarget && (
            <span data-testid={`profile-aliastarget-${label}`}>
              {t("stores.profile.aliasedToValueless", {
                canonical: input.aliasTarget,
              })}
            </span>
          )}
          {input.bestCanonical && (
            <span data-testid={`profile-bestmatch-${label}`}>
              {t("stores.profile.bestMatch", {
                canonical: input.bestCanonical,
                score: input.bestScore,
              })}
            </span>
          )}
        </>
      }
    >
      <div className="hstack" style={{ gap: 8, marginTop: 8 }}>
        <button
          type="button"
          className="chip"
          data-testid={`profile-mode-value-${label}`}
          aria-pressed={mode === "value"}
          onClick={() => onMode("value")}
        >
          {t("stores.profile.setValue")}
        </button>
        <button
          type="button"
          className="chip"
          data-testid={`profile-mode-alias-${label}`}
          aria-pressed={mode === "alias"}
          disabled={noCanonicals}
          onClick={() => onMode("alias")}
        >
          {t("stores.profile.mapToExisting")}
        </button>
      </div>

      {mode === "alias" && !noCanonicals ? (
        <>
          <select
            className="input"
            data-testid={`profile-canonical-${label}`}
            aria-label={t("stores.profile.mapToExisting")}
            value={canonical}
            onChange={(e) => onCanonical(e.target.value)}
            style={{ marginTop: 8 }}
            {...fieldErrorProps(canonicalErrorId, canonicalErrors)}
          >
            <option value="">{t("stores.profile.selectCanonical")}</option>
            {canonicalLabels.map((canonicalLabel) => (
              <option key={canonicalLabel} value={canonicalLabel}>
                {canonicalLabel}
              </option>
            ))}
          </select>
          <FieldError id={canonicalErrorId} messages={canonicalErrors} />
          <FieldError id={valueErrorId} messages={valueErrors} />
        </>
      ) : (
        <>
          <input
            className="input"
            type="text"
            data-testid={`profile-value-${label}`}
            aria-label={label}
            placeholder={t("stores.profile.valuePlaceholder")}
            value={value}
            onChange={(e) => onValue(e.target.value)}
            style={{ marginTop: 8 }}
            {...fieldErrorProps(valueErrorId, valueErrors)}
          />
          <FieldError id={valueErrorId} messages={valueErrors} />
        </>
      )}
    </RowShell>
  );
};

type ResolvedRowProps = {
  input: StoreProfileResolvedInput;
  language: string;
  value: string;
  /** Position of the row, used only to build ids — see `UnresolvedRowProps.rowIndex`. */
  rowIndex: number;
  valueErrors?: string[];
  confirming: boolean;
  onValue: (value: string) => void;
  onAskDelete: () => void;
  onCancelDelete: () => void;
  onConfirmDelete: () => void;
  onDetach: (rawLabel: string) => void;
};

const ResolvedRow = ({
  input,
  language,
  value,
  rowIndex,
  valueErrors,
  confirming,
  onValue,
  onAskDelete,
  onCancelDelete,
  onConfirmDelete,
  onDetach,
}: ResolvedRowProps) => {
  const { t } = useTranslation();
  const label = input.labelCanonical;
  const valueErrorId = `profile-resolved-error-${rowIndex}`;
  const requested = input.requestedBy.length > 0;

  return (
    <RowShell
      label={label}
      tone={requested ? "ok" : "default"}
      pillLabel={
        requested
          ? t("stores.profile.pill.resolved")
          : t("stores.profile.pill.notRequested")
      }
      meta={
        input.updatedAt
          ? t("stores.profile.updated", {
              date: formatDate(input.updatedAt, language),
            })
          : undefined
      }
    >
      {requested && (
        <div
          className="hstack"
          data-testid={`profile-fedby-${label}`}
          style={{
            gap: 6,
            marginTop: 8,
            flexWrap: "wrap",
            alignItems: "center",
          }}
        >
          <span className="t-meta t-muted">
            {t("stores.profile.fedByLabel")}
          </span>
          {input.requestedBy.map((requester) => (
            <RequesterChip
              key={requester.rawLabel}
              requester={requester}
              onDetach={() => onDetach(requester.rawLabel)}
            />
          ))}
        </div>
      )}

      <div
        className="hstack"
        style={{ gap: 8, marginTop: 8, alignItems: "center" }}
      >
        <input
          className="input"
          type="text"
          data-testid={`profile-input-${label}`}
          aria-label={label}
          placeholder={t("stores.profile.replacePlaceholder")}
          value={value}
          onChange={(e) => onValue(e.target.value)}
          {...fieldErrorProps(valueErrorId, valueErrors)}
        />
        {!confirming && (
          <button
            type="button"
            className="btn btn-ghost"
            data-testid={`profile-delete-${label}`}
            onClick={onAskDelete}
          >
            {t("stores.profile.deleteValue")}
          </button>
        )}
      </div>

      <FieldError id={valueErrorId} messages={valueErrors} />

      {confirming && (
        <div className="vstack" style={{ gap: 6, marginTop: 8 }}>
          {/* Soft copy on purpose: removing a value may leave the labels that resolve here
              still resolving, via fuzzy match against a different canonical. A precise
              count would need a full resolution simulation. */}
          {requested && (
            <span
              data-testid={`profile-delete-warning-${label}`}
              className="t-small"
              style={{ color: "var(--warn)" }}
            >
              {t("stores.profile.deleteSoftWarning", {
                count: input.requestedBy.length,
              })}
            </span>
          )}
          <div className="hstack" style={{ gap: 8 }}>
            <button
              type="button"
              className="btn btn-accent"
              data-testid={`profile-delete-confirm-${label}`}
              onClick={onConfirmDelete}
            >
              {t("stores.profile.deleteConfirm")}
            </button>
            <button
              type="button"
              className="btn btn-ghost"
              onClick={onCancelDelete}
            >
              {t("stores.profile.deleteCancel")}
            </button>
          </div>
        </div>
      )}
    </RowShell>
  );
};

const SOURCE_KEY: Record<InputResolutionSource, string> = {
  Exact: "stores.profile.sourceExact",
  Alias: "stores.profile.sourceAlias",
  Fuzzy: "stores.profile.sourceFuzzy",
};

const RequesterChip = ({
  requester,
  onDetach,
}: {
  requester: StoreProfileRequester;
  onDetach: () => void;
}) => {
  const { t } = useTranslation();
  return (
    <span
      className="chip"
      data-testid={`profile-requester-${requester.rawLabel}`}
      style={{ cursor: "default", textTransform: "none" }}
    >
      {requester.rawLabel}
      <span style={{ color: "var(--ink-3)" }}>
        {t(SOURCE_KEY[requester.source], { score: requester.confidence })}
      </span>
      {/* Only a manual alias can be detached — an exact or fuzzy match is derived, so there
          is nothing for the user to remove. */}
      {requester.source === "Alias" && (
        <button
          type="button"
          className="btn btn-ghost"
          data-testid={`profile-detach-${requester.rawLabel}`}
          aria-label={t("stores.profile.deleteAlias", {
            label: requester.rawLabel,
          })}
          onClick={onDetach}
          style={{ padding: "0 4px", height: "auto", minHeight: 0 }}
        >
          ✕
        </button>
      )}
    </span>
  );
};

export default StoreProfileEditor;
