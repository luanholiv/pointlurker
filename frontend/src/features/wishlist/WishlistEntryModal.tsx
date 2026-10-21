import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Check } from "lucide-react";
import { Link } from "react-router";
import { useAddWishlist, useUpdateWishlist } from "./useWishlist";
import Modal from "../../components/ui/Modal";
import Switch from "../../components/ui/Switch";
import FieldError from "../../components/ui/FieldError";
import { fieldErrorProps } from "../../components/ui/fieldErrorProps";
import { formatList, formatNumber } from "../../i18n/formatters";
import { anchorFields, splitFieldErrors } from "../../i18n/fieldErrors";
import type { FieldLabelMap } from "../../i18n/fieldLabels";
import type { WishlistEntryDetail } from "./wishlistApi";

/**
 * `CreateWishlistEntryRequest`'s fields. Only the redemption message has an input here — the item
 * is chosen before the modal opens — so a rule against `storeItemId` names itself at form level.
 */
const WISHLIST_LABELS: FieldLabelMap = {
  storeItemId: "wishlist.storeItem",
  redemptionMessage: "wishlist.redemptionMessage",
  desiredRedemptions: "wishlist.desiredRedemptions",
};

const WISHLIST_ANCHORS = anchorFields(
  "redemptionMessage",
  "desiredRedemptions",
);

/** Named once so the input's `aria-describedby` and the message's `id` cannot drift apart. */
const REDEMPTION_MESSAGE_ERROR_ID = "redemption-message-error";
const DESIRED_REDEMPTIONS_ERROR_ID = "desired-redemptions-error";

/** Mirrors `WishlistEntry.DesiredRedemptionsMax`; the backend rejects anything above it. */
const DESIRED_REDEMPTIONS_MAX = 10;

type WishlistEntryModalProps = {
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
  mode: "add" | "edit";
  storeItemId?: string;
  storeId?: string;
  itemName?: string;
  itemCost?: number;
  allowMessages?: boolean;
  existingEntry?: WishlistEntryDetail | null;
  isBlocked?: boolean;
  missingLabels?: string[];
};

type WishlistEntryFormProps = {
  onClose: () => void;
  onSaved: () => void;
  mode: "add" | "edit";
  storeItemId?: string;
  storeId?: string;
  itemName?: string;
  itemCost?: number;
  allowMessages: boolean;
  existingEntry?: WishlistEntryDetail | null;
  isBlocked: boolean;
  missingLabels: string[];
  status: "idle" | "saving" | "error";
  error: unknown;
  errorMessage: string;
  addEntry: ReturnType<typeof useAddWishlist>["execute"];
  updateEntry: ReturnType<typeof useUpdateWishlist>["execute"];
};

const WishlistEntryForm = ({
  onClose,
  onSaved,
  mode,
  storeItemId,
  storeId,
  itemName,
  itemCost,
  allowMessages,
  existingEntry,
  isBlocked,
  missingLabels,
  status,
  errorMessage,
  error,
  addEntry,
  updateEntry,
}: WishlistEntryFormProps) => {
  const { t, i18n } = useTranslation();
  const isEdit = mode === "edit" && Boolean(existingEntry);

  const fieldErrors = splitFieldErrors(error, {
    anchor: WISHLIST_ANCHORS,
    map: WISHLIST_LABELS,
    t,
  });

  const [notifyOnAvailable, setNotifyOnAvailable] = useState(() =>
    isEdit && existingEntry ? existingEntry.notifyOnAvailable : true,
  );
  const [autoRedeem, setAutoRedeem] = useState(() =>
    isEdit && existingEntry ? existingEntry.autoRedeem : false,
  );
  const [redemptionMessage, setRedemptionMessage] = useState(() =>
    isEdit && existingEntry ? (existingEntry.redemptionMessage ?? "") : "",
  );

  // Unlimited is its own switch rather than a magic value in the number box: it spends without a
  // ceiling, so it has to be chosen, not typed by accident.
  const [unlimited, setUnlimited] = useState(() =>
    isEdit && existingEntry !== null && existingEntry !== undefined
      ? existingEntry.desiredRedemptions === null
      : false,
  );
  const [desiredRedemptions, setDesiredRedemptions] = useState(() =>
    isEdit && existingEntry
      ? String(existingEntry.desiredRedemptions ?? 1)
      : "1",
  );

  // Kept as text while typing so a cleared box does not snap back to a number. An unreadable box
  // sends 0, which the backend rejects onto this field — never null, because null is unlimited and
  // an empty box must not be a way to ask for unlimited spending by accident.
  const parsedDesiredRedemptions = Number.parseInt(desiredRedemptions, 10);
  const desiredRedemptionsValue = unlimited
    ? null
    : Number.isNaN(parsedDesiredRedemptions)
      ? 0
      : parsedDesiredRedemptions;

  const handleSubmit = async () => {
    if (mode === "add" && storeItemId) {
      const result = await addEntry({
        storeItemId,
        notifyOnAvailable,
        autoRedeem,
        redemptionMessage: allowMessages ? redemptionMessage || null : null,
        desiredRedemptions: desiredRedemptionsValue,
      });
      if (result) {
        onSaved();
      }
    } else if (mode === "edit" && existingEntry) {
      const success = await updateEntry(existingEntry.id, {
        notifyOnAvailable,
        autoRedeem,
        redemptionMessage: allowMessages ? redemptionMessage || null : null,
        desiredRedemptions: desiredRedemptionsValue,
      });
      if (success) {
        onSaved();
      }
    }
  };

  const description = itemName
    ? itemCost !== undefined
      ? t("wishlist.modal.description", {
          name: itemName,
          cost: formatNumber(itemCost, i18n.language),
        })
      : itemName
    : undefined;

  const effectiveStoreId =
    storeId ?? (isEdit && existingEntry ? existingEntry.storeId : undefined);

  return (
    <Modal
      open={true}
      onClose={onClose}
      title={
        isEdit ? (
          <>
            {t("wishlist.modal.editTitle")}{" "}
            <em style={{ fontStyle: "italic", color: "var(--accent)" }}>
              {t("wishlist.modal.editTitleEm")}
            </em>
          </>
        ) : (
          <>
            {t("wishlist.modal.addTitle")}{" "}
            <em style={{ fontStyle: "italic", color: "var(--accent)" }}>
              {t("wishlist.modal.addTitleEm")}
            </em>
          </>
        )
      }
      description={description}
    >
      <div className="modal-body">
        {(isBlocked || missingLabels.length > 0) && (
          <div
            data-testid="wishlist-blocked-banner"
            className="t-small"
            style={{
              background: "var(--warn-soft)",
              border: "1px solid var(--warn)",
              borderRadius: "var(--r-2)",
              padding: "10px 14px",
              color: "var(--ink-2)",
            }}
          >
            <strong>{t("wishlist.autoRedeemBlocked")}</strong>{" "}
            {t("wishlist.autoRedeemBlockedDetail", {
              labels: formatList(missingLabels, i18n.language),
            })}
            {effectiveStoreId && (
              <>
                {" "}
                <Link to={`/stores/${effectiveStoreId}#profile`}>
                  {t("wishlist.autoRedeemBlockedAction")}
                </Link>
              </>
            )}
          </div>
        )}

        <div className="field">
          <div className="hstack" style={{ justifyContent: "space-between" }}>
            <span className="t-small" style={{ color: "var(--ink-2)" }}>
              {t("wishlist.notifyOnAvailable")}
            </span>
            <Switch
              checked={notifyOnAvailable}
              onChange={() => setNotifyOnAvailable((v) => !v)}
              label={t("wishlist.notifyOnAvailable")}
            />
          </div>
        </div>

        <div className="field">
          <div className="hstack" style={{ justifyContent: "space-between" }}>
            <span className="t-small" style={{ color: "var(--ink-2)" }}>
              {t("wishlist.autoRedeemWhenAvailable")}
            </span>
            <Switch
              checked={autoRedeem}
              onChange={() => setAutoRedeem((v) => !v)}
              label={t("wishlist.autoRedeemWhenAvailable")}
            />
          </div>
        </div>

        {autoRedeem && (
          <div className="field" data-testid="desired-redemptions-field">
            <div className="hstack" style={{ justifyContent: "space-between" }}>
              <span className="t-small" style={{ color: "var(--ink-2)" }}>
                {t("wishlist.desiredRedemptionsUnlimited")}
              </span>
              <Switch
                checked={unlimited}
                onChange={() => setUnlimited((v) => !v)}
                label={t("wishlist.desiredRedemptionsUnlimited")}
              />
            </div>

            {unlimited ? (
              <p className="t-small" style={{ color: "var(--ink-2)" }}>
                {t("wishlist.desiredRedemptionsUnlimitedHint")}
              </p>
            ) : (
              <>
                <label htmlFor="desired-redemptions">
                  {t("wishlist.desiredRedemptions")}
                </label>
                <input
                  id="desired-redemptions"
                  className="input"
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={DESIRED_REDEMPTIONS_MAX}
                  value={desiredRedemptions}
                  onChange={(e) => setDesiredRedemptions(e.target.value)}
                  {...fieldErrorProps(
                    DESIRED_REDEMPTIONS_ERROR_ID,
                    fieldErrors?.byField.desiredRedemptions,
                  )}
                />
                <p className="t-small" style={{ color: "var(--ink-2)" }}>
                  {t("wishlist.desiredRedemptionsHint")}
                </p>
                <FieldError
                  id={DESIRED_REDEMPTIONS_ERROR_ID}
                  messages={fieldErrors?.byField.desiredRedemptions}
                />
              </>
            )}
          </div>
        )}

        {allowMessages && (
          <div className="field">
            <label htmlFor="redemption-message">
              {t("wishlist.redemptionMessage")}
            </label>
            <textarea
              id="redemption-message"
              className="input"
              value={redemptionMessage}
              onChange={(e) => setRedemptionMessage(e.target.value)}
              rows={2}
              maxLength={1000}
              style={{ resize: "vertical", minHeight: 60 }}
              {...fieldErrorProps(
                REDEMPTION_MESSAGE_ERROR_ID,
                fieldErrors?.byField.redemptionMessage,
              )}
            />
            <FieldError
              id={REDEMPTION_MESSAGE_ERROR_ID}
              messages={fieldErrors?.byField.redemptionMessage}
            />
          </div>
        )}

        {status === "error" && fieldErrors === null && (
          <p className="t-small t-bad">{errorMessage}</p>
        )}

        {fieldErrors !== null && fieldErrors.unattached.length > 0 && (
          <div role="alert">
            {fieldErrors.unattached.map((message) => (
              <p key={message} className="t-small t-bad">
                {message}
              </p>
            ))}
          </div>
        )}
      </div>

      <Modal.Footer>
        <button className="btn" type="button" onClick={onClose}>
          {t("common.cancel")}
        </button>
        <button
          className="btn btn-accent"
          type="button"
          disabled={status === "saving"}
          onClick={handleSubmit}
        >
          <Check size={13} aria-hidden={true} focusable="false" />
          {status === "saving"
            ? t("wishlist.saving")
            : isEdit
              ? t("wishlist.saveChanges")
              : t("wishlist.addToWishlist")}
        </button>
      </Modal.Footer>
    </Modal>
  );
};

const WishlistEntryModal = ({
  open,
  onClose,
  onSaved,
  mode,
  storeItemId,
  storeId,
  itemName,
  itemCost,
  allowMessages = false,
  existingEntry,
  isBlocked = false,
  missingLabels = [],
}: WishlistEntryModalProps) => {
  const {
    execute: addEntry,
    status: addStatus,
    errorMessage: addError,
    error: addRejection,
  } = useAddWishlist();
  const {
    execute: updateEntry,
    status: updateStatus,
    errorMessage: updateError,
    error: updateRejection,
  } = useUpdateWishlist();

  const status = mode === "add" ? addStatus : updateStatus;
  const errorMessage = mode === "add" ? addError : updateError;
  const error = mode === "add" ? addRejection : updateRejection;

  if (!open) return null;
  const formKey =
    mode === "edit"
      ? `edit-${existingEntry?.id ?? "new"}`
      : `add-${storeItemId ?? "new"}`;

  const effectiveBlocked = isBlocked || (existingEntry?.isBlocked ?? false);
  const effectiveMissing =
    missingLabels.length > 0
      ? missingLabels
      : (existingEntry?.missingLabels ?? []);

  return (
    <WishlistEntryForm
      key={formKey}
      onClose={onClose}
      onSaved={onSaved}
      mode={mode}
      storeItemId={storeItemId}
      storeId={storeId}
      itemName={itemName}
      itemCost={itemCost}
      allowMessages={allowMessages}
      existingEntry={existingEntry}
      isBlocked={effectiveBlocked}
      missingLabels={effectiveMissing}
      status={status}
      errorMessage={errorMessage}
      error={error}
      addEntry={addEntry}
      updateEntry={updateEntry}
    />
  );
};

export default WishlistEntryModal;
