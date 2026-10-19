import { useState } from "react";
import { useTranslation } from "react-i18next";
import FieldError from "../../components/ui/FieldError";
import { fieldErrorProps } from "../../components/ui/fieldErrorProps";
import { useFieldErrors } from "../../hooks/useFieldErrors";
import { tApiError, tError } from "../../i18n/errorCatalog";
import { anchorFields } from "../../i18n/fieldErrors";
import type { FieldLabelMap } from "../../i18n/fieldLabels";
import { formatDate } from "../../i18n/formatters";
import { providerLabelKey } from "../stores/storeProviders";
import { useAuth } from "./AuthContext";

const SESSION_LABELS: FieldLabelMap = { secret: "settings.gauPoints.label" };
const SESSION_ANCHORS = anchorFields("secret");
const SESSION_ERROR_ID = "gaupoints-session-error";

/**
 * The GauPoints session cookie. It lives seven days and only a browser sign-in can mint one, so
 * the panel says when the stored one runs out and whether GauPoints last refused it — the owner
 * is also emailed ahead of the expiry, but this is where the new one is pasted.
 */
const GauPointsSessionPanel = () => {
  const { t, i18n } = useTranslation();
  const { user, setProviderCredential, clearProviderCredential } = useAuth();
  const [session, setSession] = useState("");
  const [status, setStatus] = useState<"idle" | "saving" | "saved" | "error">(
    "idle",
  );
  const [error, setError] = useState("");
  const fieldErrors = useFieldErrors();

  const credential = user?.credentials?.find(
    (candidate) => candidate.provider === "gaupoints",
  );
  const expiresAt = credential?.expiresAt ?? null;
  const expired = expiresAt !== null && new Date(expiresAt) <= new Date();
  const errorOptions = { anchor: SESSION_ANCHORS, map: SESSION_LABELS, t };

  const handleSave = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setStatus("saving");
    setError("");
    fieldErrors.clear();
    try {
      await setProviderCredential("gaupoints", session);
      setStatus("saved");
      setSession("");
    } catch (err) {
      setStatus("error");
      if (!fieldErrors.setFromError(err, errorOptions)) {
        setError(tApiError(err, "settings.saveTokenFailed"));
      }
    }
  };

  const handleClear = async () => {
    setStatus("saving");
    setError("");
    fieldErrors.clear();
    try {
      await clearProviderCredential("gaupoints");
      setStatus("saved");
    } catch (err) {
      setStatus("error");
      setError(tApiError(err, "settings.clearTokenFailed"));
    }
  };

  return (
    <div className="panel" data-testid="gaupoints-session-panel">
      <div className="panel-head">
        <h2 className="t-heading">{t(providerLabelKey("gaupoints"))}</h2>
        {credential === undefined ? (
          <span className="pill warn">
            <span className="dot" />
            {t("settings.gauPoints.notSet")}
          </span>
        ) : expired || credential.lastValidationError !== null ? (
          <span className="pill bad">
            <span className="dot" />
            {expired
              ? t("settings.gauPoints.expired")
              : t("settings.gauPoints.refused")}
          </span>
        ) : (
          <span className="pill ok">
            <span className="dot" />
            {t("settings.gauPoints.configured")}
          </span>
        )}
      </div>

      <p className="t-small t-muted" style={{ marginBottom: 12 }}>
        {t("settings.gauPoints.howTo")}
      </p>

      {credential !== undefined && (
        <div className="vstack" style={{ gap: 4, marginBottom: 12 }}>
          {credential.accountName !== null && (
            <p className="t-small">
              {t("settings.gauPoints.account", {
                account: credential.accountName,
              })}
            </p>
          )}
          {expiresAt !== null && (
            <p className={expired ? "t-small t-bad" : "t-small"}>
              {t(
                expired
                  ? "settings.gauPoints.expiredAt"
                  : "settings.gauPoints.expiresAt",
                {
                  when: formatDate(expiresAt, i18n.language, {
                    dateStyle: "medium",
                    timeStyle: "short",
                  }),
                },
              )}
            </p>
          )}
          {credential.lastValidationError !== null && (
            <p className="t-small t-bad">
              {tError(credential.lastValidationError, {}, "")}
            </p>
          )}
        </div>
      )}

      <form className="vstack" style={{ gap: 12 }} onSubmit={handleSave}>
        <div className="field">
          <label className="t-eyebrow" htmlFor="gaupoints-session-input">
            {t("settings.gauPoints.label")}
          </label>
          <input
            id="gaupoints-session-input"
            className="input"
            type="password"
            autoComplete="off"
            placeholder={t("settings.gauPoints.placeholder")}
            value={session}
            onChange={(e) => setSession(e.target.value)}
            required
            {...fieldErrorProps(SESSION_ERROR_ID, fieldErrors.byField.secret)}
          />
          <FieldError
            id={SESSION_ERROR_ID}
            messages={fieldErrors.byField.secret}
          />
        </div>
        {fieldErrors.unattached.length > 0 && (
          <div role="alert">
            {fieldErrors.unattached.map((message) => (
              <p key={message} className="t-small t-bad">
                {message}
              </p>
            ))}
          </div>
        )}
        {status === "error" && error !== "" && (
          <p role="alert" className="t-small t-bad">
            {error}
          </p>
        )}
        {status === "saved" && (
          <p className="t-small t-ok">{t("settings.gauPoints.saved")}</p>
        )}
        <div className="hstack" style={{ gap: 8 }}>
          <button
            className="btn btn-accent"
            type="submit"
            disabled={status === "saving"}
          >
            {status === "saving"
              ? t("settings.savingToken")
              : t("settings.gauPoints.save")}
          </button>
          {credential !== undefined && (
            <button
              className="btn btn-danger"
              type="button"
              onClick={handleClear}
              disabled={status === "saving"}
            >
              {t("settings.gauPoints.clear")}
            </button>
          )}
        </div>
      </form>
    </div>
  );
};

export default GauPointsSessionPanel;
