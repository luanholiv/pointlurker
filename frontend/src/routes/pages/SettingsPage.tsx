import { useState } from "react";
import { useTranslation } from "react-i18next";
import { QRCodeSVG } from "qrcode.react";
import { Modal, PageHead, Tabs } from "../../components/ui";
import FieldError from "../../components/ui/FieldError";
import { fieldErrorProps } from "../../components/ui/fieldErrorProps";
import { useFieldErrors } from "../../hooks/useFieldErrors";
import { useHashTab } from "../../hooks/useHashTab";
import { useAuth } from "../../features/auth/AuthContext";
import GauPointsSessionPanel from "../../features/auth/GauPointsSessionPanel";
import {
  providerLabelKey,
  STORE_PROVIDERS,
  type StoreProvider,
} from "../../features/stores/storeProviders";
import { SUPPORTED_LOCALES } from "../../i18n";
import { tApiError } from "../../i18n/errorCatalog";
import { anchorFields } from "../../i18n/fieldErrors";
import type { FieldLabelMap } from "../../i18n/fieldLabels";

/**
 * Each of this page's three forms validates one field, so each anchors that one field to its input.
 * The labels are the ones the inputs already carry, which is what keeps a message that cannot be
 * attached — a field this form does not render — readable at form level.
 */
const CREDENTIAL_FIELD = "secret";
const CREDENTIAL_LABELS: FieldLabelMap = {
  [CREDENTIAL_FIELD]: "settings.jwtTokenLabel",
};
const CREDENTIAL_ANCHORS = anchorFields(CREDENTIAL_FIELD);

const NEW_PW_FIELD = "password";
const NEW_PW_LABELS: FieldLabelMap = { [NEW_PW_FIELD]: "settings.newPassword" };
const NEW_PW_ANCHORS = anchorFields(NEW_PW_FIELD);

const LOCALE_LABELS: FieldLabelMap = { locale: "settings.language" };
const LOCALE_ANCHORS = anchorFields("locale");

const TWO_FACTOR_LABELS: FieldLabelMap = {
  twoFactorCode: "settings.verificationCode",
};
const TWO_FACTOR_ANCHORS = anchorFields("twoFactorCode");

/** Named once so each input's `aria-describedby` and its message's `id` cannot drift apart. */
const SE_TOKEN_ERROR_ID = "se-token-error";
const NEW_PASSWORD_ERROR_ID = "new-pw-error";
const LOCALE_ERROR_ID = "locale-error";
const TWO_FACTOR_ERROR_ID = "totp-code-error";

/** Names the recovery-code list so it is reachable as a list rather than loose monospace text. */
const RECOVERY_CODES_LABEL_ID = "recovery-codes-label";

/**
 * A QR keeps its own colours in both themes: the format needs dark modules on a light ground with
 * a quiet zone around them, and a scanner reading it off a dark-theme panel would find neither.
 */
const QR_SIZE = 168;
const QR_BACKGROUND = "#FFFFFF";
const QR_FOREGROUND = "#000000";

const LOCALE_NAMES: Record<string, string> = {
  "en-US": "English",
  "pt-BR": "Português (Brasil)",
};

const SettingsPage = () => {
  const { t, i18n } = useTranslation();
  const {
    user,
    changePassword,
    enableTwoFactor,
    verifyTwoFactorEnrollment,
    disableTwoFactor,
    setStreamElementsToken,
    clearStreamElementsToken,
    changeLocale,
  } = useAuth();

  const { activeTab, selectTab } = useHashTab(
    ["account", "token", "security"],
    "account",
  );

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordStatus, setPasswordStatus] = useState<
    "idle" | "saving" | "saved" | "error"
  >("idle");
  const [passwordError, setPasswordError] = useState("");

  const [seToken, setSeToken] = useState("");
  const [seTokenStatus, setSeTokenStatus] = useState<
    "idle" | "saving" | "saved" | "error"
  >("idle");
  const [seTokenError, setSeTokenError] = useState("");

  const [localeStatus, setLocaleStatus] = useState<
    "idle" | "saving" | "saved" | "error"
  >("idle");
  const [localeError, setLocaleError] = useState("");

  const tokenFieldErrors = useFieldErrors();
  const passwordFieldErrors = useFieldErrors();
  const localeFieldErrors = useFieldErrors();

  const [twoFactorSetup, setTwoFactorSetup] = useState<{
    sharedKey: string;
    otpauthUri: string;
  } | null>(null);
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [recoveryCodes, setRecoveryCodes] = useState<string[] | null>(null);
  const [disableConfirmOpen, setDisableConfirmOpen] = useState(false);
  const [twoFactorStatus, setTwoFactorStatus] = useState<
    "idle" | "saving" | "error"
  >("idle");
  const [twoFactorError, setTwoFactorError] = useState("");
  const twoFactorFieldErrors = useFieldErrors();

  const configuredProviders = new Set<StoreProvider>(
    (user?.credentials ?? []).map((credential) => credential.provider),
  );
  const hasSeToken = configuredProviders.has("streamelements");
  const twoFactorEnabled = user?.twoFactorEnabled ?? false;

  const tokenErrorOptions = {
    anchor: CREDENTIAL_ANCHORS,
    map: CREDENTIAL_LABELS,
    t,
  };

  const handleSaveSeToken = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setSeTokenStatus("saving");
    setSeTokenError("");
    tokenFieldErrors.clear();
    try {
      await setStreamElementsToken(seToken);
      setSeTokenStatus("saved");
      setSeToken("");
    } catch (error) {
      setSeTokenStatus("error");
      if (!tokenFieldErrors.setFromError(error, tokenErrorOptions)) {
        setSeTokenError(tApiError(error, "settings.saveTokenFailed"));
      }
    }
  };

  const handleClearSeToken = async () => {
    setSeTokenStatus("saving");
    setSeTokenError("");
    tokenFieldErrors.clear();
    try {
      await clearStreamElementsToken();
      setSeTokenStatus("saved");
    } catch (error) {
      setSeTokenStatus("error");
      if (!tokenFieldErrors.setFromError(error, tokenErrorOptions)) {
        setSeTokenError(tApiError(error, "settings.clearTokenFailed"));
      }
    }
  };

  const handlePasswordChange = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    passwordFieldErrors.clear();
    if (newPassword !== confirmPassword) {
      setPasswordStatus("error");
      setPasswordError(t("settings.passwordsMismatch"));
      return;
    }
    setPasswordStatus("saving");
    setPasswordError("");
    try {
      await changePassword({ currentPassword, newPassword });
      setPasswordStatus("saved");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    } catch (error) {
      setPasswordStatus("error");
      if (
        !passwordFieldErrors.setFromError(error, {
          anchor: NEW_PW_ANCHORS,
          map: NEW_PW_LABELS,
          t,
        })
      ) {
        setPasswordError(tApiError(error, "settings.updateFailed"));
      }
    }
  };

  const twoFactorErrorOptions = {
    anchor: TWO_FACTOR_ANCHORS,
    map: TWO_FACTOR_LABELS,
    t,
  };

  /**
   * All three share one error slot because the panel only ever offers one of them at a
   * time, and each clears the last failure before trying again.
   */
  const reportTwoFactorFailure = (error: unknown, fallbackKey: string) => {
    setTwoFactorStatus("error");
    if (!twoFactorFieldErrors.setFromError(error, twoFactorErrorOptions)) {
      setTwoFactorError(tApiError(error, fallbackKey));
    }
  };

  const beginTwoFactorAction = () => {
    setTwoFactorStatus("saving");
    setTwoFactorError("");
    twoFactorFieldErrors.clear();
  };

  const handleEnableTwoFactor = async () => {
    beginTwoFactorAction();
    try {
      const setup = await enableTwoFactor();
      setTwoFactorSetup(setup);
      setTwoFactorStatus("idle");
    } catch (error) {
      reportTwoFactorFailure(error, "settings.enable2FAFailed");
    }
  };

  const handleVerifyTwoFactor = async () => {
    beginTwoFactorAction();
    try {
      const { recoveryCodes: issued } =
        await verifyTwoFactorEnrollment(twoFactorCode);
      setRecoveryCodes(issued);
      setTwoFactorSetup(null);
      setTwoFactorCode("");
      setTwoFactorStatus("idle");
    } catch (error) {
      reportTwoFactorFailure(error, "settings.verify2FAFailed");
    }
  };

  const handleDisableTwoFactor = async () => {
    beginTwoFactorAction();
    try {
      await disableTwoFactor();
      setRecoveryCodes(null);
      setTwoFactorStatus("idle");
    } catch (error) {
      reportTwoFactorFailure(error, "settings.disable2FAFailed");
    } finally {
      // The confirmation closes either way: the failure belongs on the panel behind it, which the
      // dialog would otherwise cover.
      setDisableConfirmOpen(false);
    }
  };

  const handleChangeLocale = async (locale: string) => {
    setLocaleStatus("saving");
    setLocaleError("");
    localeFieldErrors.clear();
    try {
      await changeLocale(locale);
      setLocaleStatus("saved");
    } catch (error) {
      setLocaleStatus("error");
      if (
        !localeFieldErrors.setFromError(error, {
          anchor: LOCALE_ANCHORS,
          map: LOCALE_LABELS,
          t,
        })
      ) {
        setLocaleError(tApiError(error, "settings.localeError"));
      }
    }
  };

  return (
    <div className="page">
      <PageHead
        title={t("settings.title")}
        description={t("settings.description")}
      />

      <Tabs
        tabs={[
          { id: "account", label: t("settings.account") },
          { id: "token", label: t("settings.credentialsTab") },
          { id: "security", label: t("settings.security") },
        ]}
        activeId={activeTab}
        onSelect={selectTab}
      />

      {activeTab === "account" && (
        <div className="vstack" style={{ gap: 18 }} role="tabpanel">
          <div className="panel">
            <div className="panel-head">
              <h2 className="t-heading">{t("settings.identity")}</h2>
            </div>
            <div className="vstack" style={{ gap: 10 }}>
              <div className="between">
                <span className="t-eyebrow">{t("auth.email")}</span>
                <span>{user?.email ?? "—"}</span>
              </div>
              <div className="between">
                <span className="t-eyebrow">{t("settings.userId")}</span>
                <span className="t-meta">{user?.userId ?? "—"}</span>
              </div>
              {STORE_PROVIDERS.map((provider) => (
                <div key={provider} className="between">
                  <span className="t-eyebrow">
                    {t(providerLabelKey(provider))}
                  </span>
                  <span className="t-meta">
                    {configuredProviders.has(provider)
                      ? t("settings.credentialConfigured")
                      : t("settings.credentialNotSet")}
                  </span>
                </div>
              ))}
            </div>
          </div>

          <div className="panel">
            <div className="panel-head">
              <h2 className="t-heading">{t("settings.language")}</h2>
            </div>
            <div className="hstack" style={{ gap: 10, alignItems: "center" }}>
              <select
                className="select"
                value={i18n.language}
                onChange={(e) => void handleChangeLocale(e.target.value)}
                disabled={localeStatus === "saving"}
                aria-label={t("settings.changeLocale")}
                {...fieldErrorProps(
                  LOCALE_ERROR_ID,
                  localeFieldErrors.byField.locale,
                )}
              >
                {SUPPORTED_LOCALES.map((locale) => (
                  <option key={locale} value={locale}>
                    {LOCALE_NAMES[locale] ?? locale}
                  </option>
                ))}
              </select>
              {localeStatus === "saved" && (
                <span className="t-small t-ok">
                  {t("settings.localeSaved")}
                </span>
              )}
              {localeStatus === "error" && localeError !== "" && (
                <span className="t-small t-bad">{localeError}</span>
              )}
              <FieldError
                id={LOCALE_ERROR_ID}
                messages={localeFieldErrors.byField.locale}
              />
              {localeFieldErrors.unattached.map((message) => (
                <span key={message} role="alert" className="t-small t-bad">
                  {message}
                </span>
              ))}
            </div>
          </div>
        </div>
      )}

      {activeTab === "token" && (
        <div className="vstack" style={{ gap: 18 }} role="tabpanel">
          <div className="panel">
            <div className="panel-head">
              <h2 className="t-heading">
                {t(providerLabelKey("streamelements"))}
              </h2>
              {hasSeToken ? (
                <span className="pill ok">
                  <span className="dot" />
                  {t("settings.credentialConfigured")}
                </span>
              ) : (
                <span className="pill warn">
                  <span className="dot" />
                  {t("settings.credentialNotSet")}
                </span>
              )}
            </div>

            <p className="t-small t-muted" style={{ marginBottom: 12 }}>
              {t("settings.streamElements.howTo")}
            </p>

            <form
              className="vstack"
              style={{ gap: 12 }}
              onSubmit={handleSaveSeToken}
            >
              <div className="field">
                <label className="t-eyebrow" htmlFor="se-token-input">
                  {t("settings.jwtTokenLabel")}
                </label>
                <input
                  id="se-token-input"
                  className="input"
                  type="password"
                  placeholder={t("settings.jwtTokenPlaceholder")}
                  value={seToken}
                  onChange={(e) => setSeToken(e.target.value)}
                  required
                  {...fieldErrorProps(
                    SE_TOKEN_ERROR_ID,
                    tokenFieldErrors.byField.secret,
                  )}
                />
                <FieldError
                  id={SE_TOKEN_ERROR_ID}
                  messages={tokenFieldErrors.byField.secret}
                />
              </div>
              {tokenFieldErrors.unattached.length > 0 && (
                <div role="alert">
                  {tokenFieldErrors.unattached.map((message) => (
                    <p key={message} className="t-small t-bad">
                      {message}
                    </p>
                  ))}
                </div>
              )}
              {seTokenStatus === "error" && seTokenError !== "" && (
                <p className="t-small t-bad">{seTokenError}</p>
              )}
              {seTokenStatus === "saved" && (
                <p className="t-small t-ok">{t("settings.tokenSaved")}</p>
              )}
              <div className="hstack" style={{ gap: 8 }}>
                <button
                  className="btn btn-accent"
                  type="submit"
                  disabled={seTokenStatus === "saving"}
                >
                  {seTokenStatus === "saving"
                    ? t("settings.savingToken")
                    : t("settings.saveToken")}
                </button>
                {hasSeToken && (
                  <button
                    className="btn btn-danger"
                    type="button"
                    onClick={handleClearSeToken}
                    disabled={seTokenStatus === "saving"}
                  >
                    {t("settings.clearToken")}
                  </button>
                )}
              </div>
            </form>
          </div>
          <GauPointsSessionPanel />
        </div>
      )}

      {activeTab === "security" && (
        <div className="vstack" style={{ gap: 18 }} role="tabpanel">
          <div className="panel">
            <div className="panel-head">
              <h2 className="t-heading">
                {t("settings.changePasswordHeading")}
              </h2>
            </div>
            <form
              className="vstack"
              style={{ gap: 12 }}
              onSubmit={handlePasswordChange}
            >
              <div className="field">
                <label className="t-eyebrow" htmlFor="current-pw">
                  {t("settings.currentPassword")}
                </label>
                <input
                  id="current-pw"
                  className="input"
                  type="password"
                  value={currentPassword}
                  onChange={(e) => setCurrentPassword(e.target.value)}
                  required
                />
              </div>
              <div className="field">
                <label className="t-eyebrow" htmlFor="new-pw">
                  {t("settings.newPassword")}
                </label>
                <input
                  id="new-pw"
                  className="input"
                  type="password"
                  value={newPassword}
                  onChange={(e) => setNewPassword(e.target.value)}
                  required
                  {...fieldErrorProps(
                    NEW_PASSWORD_ERROR_ID,
                    passwordFieldErrors.byField.password,
                  )}
                />
                <FieldError
                  id={NEW_PASSWORD_ERROR_ID}
                  messages={passwordFieldErrors.byField.password}
                />
              </div>
              <div className="field">
                <label className="t-eyebrow" htmlFor="confirm-pw">
                  {t("settings.confirmNewPassword")}
                </label>
                <input
                  id="confirm-pw"
                  className="input"
                  type="password"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  required
                />
              </div>
              {passwordFieldErrors.unattached.length > 0 && (
                <div role="alert">
                  {passwordFieldErrors.unattached.map((message) => (
                    <p key={message} className="t-small t-bad">
                      {message}
                    </p>
                  ))}
                </div>
              )}
              {passwordStatus === "error" && passwordError !== "" && (
                <p className="t-small t-bad">{passwordError}</p>
              )}
              {passwordStatus === "saved" && (
                <p className="t-small t-ok">{t("settings.passwordSaved")}</p>
              )}
              <button
                className="btn btn-accent"
                type="submit"
                disabled={passwordStatus === "saving"}
              >
                {passwordStatus === "saving"
                  ? t("settings.savingPassword")
                  : t("settings.updatePassword")}
              </button>
            </form>
          </div>

          <div className="panel">
            <div className="panel-head">
              <h2 className="t-heading">{t("settings.twoFactorHeading")}</h2>
            </div>

            {twoFactorEnabled ? (
              <div
                className="hstack"
                style={{ justifyContent: "space-between" }}
              >
                <span className="t-small">
                  {t("settings.twoFactorEnabled")}
                </span>
                <button
                  className="btn btn-ghost"
                  type="button"
                  disabled={twoFactorStatus === "saving"}
                  onClick={() => setDisableConfirmOpen(true)}
                >
                  {t("settings.disable2FA")}
                </button>
              </div>
            ) : (
              <button
                className="btn"
                type="button"
                disabled={twoFactorStatus === "saving"}
                onClick={() => void handleEnableTwoFactor()}
              >
                {t("settings.enable2FA")}
              </button>
            )}

            {twoFactorSetup && (
              <div className="vstack" style={{ gap: 10, marginTop: 12 }}>
                <p className="t-eyebrow">{t("settings.scanKey")}</p>
                <div
                  style={{
                    background: QR_BACKGROUND,
                    padding: 12,
                    borderRadius: 8,
                    width: "fit-content",
                  }}
                >
                  {/* `role="img"` pairs with `title` to give the QR an accessible name — a bare
                      <svg> maps inconsistently across browsers, leaving the only enrolment
                      affordance unnamed to a screen reader. */}
                  <QRCodeSVG
                    role="img"
                    value={twoFactorSetup.otpauthUri}
                    size={QR_SIZE}
                    bgColor={QR_BACKGROUND}
                    fgColor={QR_FOREGROUND}
                    marginSize={2}
                    title={t("settings.scanKey")}
                  />
                </div>
                <p className="t-eyebrow">{t("settings.orEnterKey")}</p>
                <code className="t-meta">{twoFactorSetup.sharedKey}</code>
                <div className="field">
                  <label className="t-eyebrow" htmlFor="totp-code">
                    {t("settings.verificationCode")}
                  </label>
                  <input
                    id="totp-code"
                    className="input"
                    type="text"
                    value={twoFactorCode}
                    onChange={(e) => setTwoFactorCode(e.target.value)}
                    {...fieldErrorProps(
                      TWO_FACTOR_ERROR_ID,
                      twoFactorFieldErrors.byField.twoFactorCode,
                    )}
                  />
                  <FieldError
                    id={TWO_FACTOR_ERROR_ID}
                    messages={twoFactorFieldErrors.byField.twoFactorCode}
                  />
                </div>
                <button
                  className="btn btn-accent"
                  type="button"
                  disabled={twoFactorStatus === "saving"}
                  onClick={() => void handleVerifyTwoFactor()}
                >
                  {t("settings.verifyAndEnable")}
                </button>
              </div>
            )}

            {recoveryCodes !== null && recoveryCodes.length > 0 && (
              <div className="vstack" style={{ gap: 8, marginTop: 12 }}>
                <p className="t-eyebrow" id={RECOVERY_CODES_LABEL_ID}>
                  {t("settings.recoveryCodesHeading")}
                </p>
                <p className="t-small">{t("settings.recoveryCodesIntro")}</p>
                <ul
                  aria-labelledby={RECOVERY_CODES_LABEL_ID}
                  className="t-meta"
                  style={{
                    columns: 2,
                    listStyle: "none",
                    margin: 0,
                    padding: 0,
                  }}
                >
                  {recoveryCodes.map((recoveryCode) => (
                    <li key={recoveryCode}>{recoveryCode}</li>
                  ))}
                </ul>
                <button
                  className="btn btn-ghost"
                  type="button"
                  onClick={() => setRecoveryCodes(null)}
                >
                  {t("settings.recoveryCodesAcknowledge")}
                </button>
              </div>
            )}

            {twoFactorFieldErrors.unattached.length > 0 && (
              <div role="alert" style={{ marginTop: 12 }}>
                {twoFactorFieldErrors.unattached.map((message) => (
                  <p key={message} className="t-small t-bad">
                    {message}
                  </p>
                ))}
              </div>
            )}
            {twoFactorStatus === "error" && twoFactorError !== "" && (
              <p
                role="alert"
                className="t-small t-bad"
                style={{ marginTop: 12 }}
              >
                {twoFactorError}
              </p>
            )}

            <Modal
              open={disableConfirmOpen}
              onClose={() => setDisableConfirmOpen(false)}
              title={t("settings.disable2FAConfirmTitle")}
              description={t("settings.disable2FAConfirmBody")}
            >
              <Modal.Footer>
                <button
                  className="btn"
                  type="button"
                  onClick={() => setDisableConfirmOpen(false)}
                >
                  {t("common.cancel")}
                </button>
                <button
                  className="btn btn-accent"
                  type="button"
                  disabled={twoFactorStatus === "saving"}
                  onClick={() => void handleDisableTwoFactor()}
                >
                  {t("settings.disable2FAConfirmAction")}
                </button>
              </Modal.Footer>
            </Modal>
          </div>

          <div className="panel panel-danger">
            <div className="panel-head">
              <h2 className="t-heading">{t("settings.sessionsHeading")}</h2>
            </div>
            <p className="t-small t-muted">
              {t("settings.sessionsDescription")}
            </p>
            <button
              className="btn btn-danger"
              type="button"
              style={{ marginTop: 12 }}
            >
              {t("settings.revokeAllSessions")}
            </button>
          </div>
        </div>
      )}
    </div>
  );
};

export default SettingsPage;
