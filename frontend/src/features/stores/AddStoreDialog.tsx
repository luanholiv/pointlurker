import { Check } from "lucide-react";
import { useTranslation } from "react-i18next";
import Modal from "../../components/ui/Modal";
import Pill from "../../components/ui/Pill";
import { anchorNothing, splitFieldErrors } from "../../i18n/fieldErrors";
import type { FieldLabelMap } from "../../i18n/fieldLabels";
import { useStoreLookup, useCreateStore } from "./useStores";
import {
  isSingleStoreProvider,
  providerLabelKey,
  STORE_PROVIDERS,
  type StoreProvider,
} from "./storeProviders";
import { useMemo, useState } from "react";

/**
 * `CreateStoreRequest`'s fields, named the way the user would.
 *
 * None has an input on this dialog — the identity is taken from the lookup — so a rule that breaks
 * one of them has nowhere to attach and is reported at form level. Naming it there is the only
 * thing that distinguishes them, since several resolve to the same field-agnostic sentence.
 */
const CREATE_STORE_LABELS: FieldLabelMap = {
  provider: "stores.provider",
  channelId: "stores.channelId",
  displayName: "stores.displayName",
};

type AddStoreDialogProps = {
  open: boolean;
  onClose: () => void;
  onCreated: (result: {
    storeId: string;
    storeName: string;
    missingLabels: string[];
  }) => void;
  /** Providers the owner holds a credential for. A store needs its own provider's credential. */
  credentialProviders: readonly StoreProvider[];
  /**
   * Single-store providers whose store is already watched. The picker disables them as a
   * convenience only: the server refuses a second one whatever the UI shows.
   */
  addedSingleStoreProviders: readonly StoreProvider[];
};

const AddStoreDialog = ({
  open,
  onClose,
  onCreated,
  credentialProviders,
  addedSingleStoreProviders,
}: AddStoreDialogProps) => {
  const { t } = useTranslation();
  const [provider, setProvider] = useState<StoreProvider>("streamelements");
  const [username, setUsername] = useState("");
  const {
    execute: createStore,
    status: createStatus,
    errorMessage,
    error: createError,
    reset: resetCreate,
  } = useCreateStore();
  const { state: lookupState, lookup, reset: resetLookup } = useStoreLookup();

  // `anchorNothing` on purpose: this dialog has no input for any validated field.
  const createFieldErrors = useMemo(
    () =>
      splitFieldErrors(createError, {
        anchor: anchorNothing,
        map: CREATE_STORE_LABELS,
        t,
      }),
    [createError, t],
  );

  const singleStore = isSingleStoreProvider(provider);
  const hasCredential = credentialProviders.includes(provider);
  const alreadyAdded = addedSingleStoreProviders.includes(provider);
  const providerName = t(providerLabelKey(provider));

  const isResolved =
    lookupState.status === "resolved" && lookupState.provider === provider;
  const isLookingUp = lookupState.status === "loading";

  const selectProvider = (next: StoreProvider) => {
    if (next === provider) return;
    setProvider(next);
    setUsername("");
    resetLookup();
    resetCreate();
    // Nothing to ask for a fixed store, so its lookup starts as soon as it is picked.
    if (
      isSingleStoreProvider(next) &&
      credentialProviders.includes(next) &&
      !addedSingleStoreProviders.includes(next)
    ) {
      void lookup(next);
    }
  };

  const handleUsernameChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    setUsername(e.target.value);
    if (lookupState.status === "resolved") {
      resetLookup();
    }
  };

  const handleLookup = async () => {
    if (!username.trim()) return;
    await lookup(provider, username.trim());
  };

  const reset = () => {
    setProvider("streamelements");
    setUsername("");
    resetLookup();
    resetCreate();
  };

  const handleAdd = async () => {
    if (lookupState.status !== "resolved") return;
    const result = await createStore(
      lookupState.provider === "streamelements"
        ? {
            provider: "streamelements",
            channelId: lookupState.externalId,
            displayName: lookupState.displayName,
          }
        : { provider: lookupState.provider },
    );
    if (result) {
      reset();
      onCreated({
        storeId: result.id,
        storeName: result.displayName,
        missingLabels: result.missingLabels ?? [],
      });
    }
  };

  const handleClose = () => {
    reset();
    onClose();
  };

  const notice = !hasCredential
    ? t("stores.addDialog.credentialMissing", { provider: providerName })
    : alreadyAdded
      ? t("stores.addDialog.alreadyAdded", { provider: providerName })
      : null;

  return (
    <Modal
      open={open}
      onClose={handleClose}
      eyebrow={t("stores.addDialog.eyebrow")}
      title={
        <>
          {t("common.add")}{" "}
          <em style={{ fontStyle: "italic", color: "var(--accent)" }}>
            {t("stores.addDialog.titleEm")}
          </em>
        </>
      }
      description={
        singleStore
          ? t("stores.addDialog.descriptionFixed", { provider: providerName })
          : t("stores.addDialog.description")
      }
    >
      <div className="modal-body">
        <div
          className="hstack"
          role="group"
          aria-label={t("stores.provider")}
          style={{ gap: 6, flexWrap: "wrap" }}
        >
          {STORE_PROVIDERS.map((option) => (
            <button
              key={option}
              type="button"
              className="chip"
              aria-pressed={option === provider}
              onClick={() => selectProvider(option)}
            >
              {t(providerLabelKey(option))}
            </button>
          ))}
        </div>

        {notice !== null && (
          <p role="status" className="t-small" style={{ color: "var(--warn)" }}>
            {notice}
          </p>
        )}

        {!singleStore && (
          <div className="field">
            <label htmlFor="channel-username">
              {t("stores.channelUsername")}
            </label>
            <div className="input-row">
              <input
                id="channel-username"
                className="input"
                type="text"
                value={username}
                onChange={handleUsernameChange}
                placeholder={t("stores.addDialog.usernamePlaceholder")}
                disabled={!hasCredential}
                aria-describedby={
                  lookupState.status === "error" ? "lookup-error" : undefined
                }
                aria-invalid={lookupState.status === "error" || undefined}
              />
              <button
                type="button"
                className="btn"
                onClick={handleLookup}
                disabled={isLookingUp || !username.trim() || !hasCredential}
              >
                {isLookingUp ? t("stores.lookingUp") : t("stores.lookup")}
              </button>
            </div>
          </div>
        )}

        {singleStore && isLookingUp && (
          <p className="t-small t-muted">{t("stores.lookingUp")}</p>
        )}

        {lookupState.status === "error" && (
          <p id="lookup-error" className="t-small t-bad">
            {lookupState.message}
          </p>
        )}

        {lookupState.status === "resolved" && isResolved && (
          <div className="card" style={{ padding: 14 }}>
            <div className="between">
              <div>
                <div className="t-subheading">{lookupState.displayName}</div>
                <div className="t-meta t-muted" style={{ marginTop: 4 }}>
                  {lookupState.itemCount === null
                    ? t("stores.addDialog.storeId", {
                        id: lookupState.externalId,
                      })
                    : t("stores.itemCount", { count: lookupState.itemCount })}
                </div>
              </div>
              <Pill tone="ok">{t("stores.foundPill")}</Pill>
            </div>
          </div>
        )}

        {createStatus === "error" &&
          (createFieldErrors === null ? (
            <p className="t-small t-bad">{errorMessage}</p>
          ) : (
            <div role="alert">
              {createFieldErrors.unattached.map((message) => (
                <p key={message} className="t-small t-bad">
                  {message}
                </p>
              ))}
            </div>
          ))}
      </div>

      <Modal.Footer>
        <button className="btn" onClick={handleClose}>
          {t("common.cancel")}
        </button>
        <button
          className="btn btn-accent"
          disabled={!isResolved || createStatus === "saving" || notice !== null}
          onClick={handleAdd}
        >
          <Check size={13} aria-hidden={true} focusable="false" />{" "}
          {t("stores.addDialog.continue")}
        </button>
      </Modal.Footer>
    </Modal>
  );
};

export default AddStoreDialog;
