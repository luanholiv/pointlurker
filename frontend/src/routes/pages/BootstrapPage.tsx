import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import FieldError from "../../components/ui/FieldError";
import { fieldErrorProps } from "../../components/ui/fieldErrorProps";
import { useAuth } from "../../features/auth/AuthContext";
import { useFieldErrors } from "../../hooks/useFieldErrors";
import { tApiError } from "../../i18n/errorCatalog";
import { anchorFields } from "../../i18n/fieldErrors";
import type { FieldLabelMap } from "../../i18n/fieldLabels";

/** The fields Identity reports against registration, named the way this form labels them. */
const REGISTER_LABELS: FieldLabelMap = {
  email: "auth.email",
  password: "auth.password",
};

const REGISTER_ANCHORS = anchorFields("email", "password");

/** Named once so each input's `aria-describedby` and its message's `id` cannot drift apart. */
const EMAIL_ERROR_ID = "bp-email-error";
const PASSWORD_ERROR_ID = "bp-password-error";

const BootstrapPage = () => {
  const navigate = useNavigate();
  const { register } = useAuth();
  const { t } = useTranslation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");
  const fieldErrors = useFieldErrors();

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    fieldErrors.clear();
    if (password !== confirmPassword) {
      setStatus("error");
      setErrorMessage(t("auth.passwordsMismatch"));
      return;
    }
    setStatus("saving");
    setErrorMessage("");
    try {
      await register({ email, password });
      navigate("/login", { replace: true });
    } catch (error) {
      setStatus("error");
      if (
        !fieldErrors.setFromError(error, {
          anchor: REGISTER_ANCHORS,
          map: REGISTER_LABELS,
          t,
        })
      ) {
        setErrorMessage(tApiError(error, "auth.createAdminFailed"));
      }
    }
  };

  return (
    <div className="auth-simple">
      <main>
        <div className="auth-card">
          <div className="auth-brand">
            <img src="/icon.svg?v=2" className="brand-mark" alt="" />
            <div>
              <div className="brand-name">{t("common.brandName")}</div>
              <div className="brand-sub">{t("common.brandSub")}</div>
            </div>
          </div>

          <div className="t-eyebrow" style={{ marginBottom: 12 }}>
            {t("auth.bootstrapEyebrow")}
          </div>
          <h1 className="t-hero" style={{ marginBottom: 6 }}>
            {t("auth.createAdmin")}
          </h1>
          <p className="t-muted" style={{ margin: "0 0 22px" }}>
            {t("auth.bootstrapDescription")}
          </p>

          <form className="vstack" style={{ gap: 14 }} onSubmit={handleSubmit}>
            <div className="field">
              <label htmlFor="bp-email">{t("auth.email")}</label>
              <input
                id="bp-email"
                className="input"
                type="email"
                placeholder={t("auth.emailPlaceholder")}
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                {...fieldErrorProps(EMAIL_ERROR_ID, fieldErrors.byField.email)}
              />
              <FieldError
                id={EMAIL_ERROR_ID}
                messages={fieldErrors.byField.email}
              />
            </div>
            <div className="field">
              <label htmlFor="bp-password">{t("auth.password")}</label>
              <input
                id="bp-password"
                className="input"
                type="password"
                placeholder={t("auth.passwordPlaceholder")}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
                {...fieldErrorProps(
                  PASSWORD_ERROR_ID,
                  fieldErrors.byField.password,
                )}
              />
              <FieldError
                id={PASSWORD_ERROR_ID}
                messages={fieldErrors.byField.password}
              />
            </div>
            <div className="field">
              <label htmlFor="bp-confirm">{t("auth.confirmNewPassword")}</label>
              <input
                id="bp-confirm"
                className="input"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
              />
            </div>

            {status === "error" && errorMessage !== "" && (
              <p className="t-small t-bad">{errorMessage}</p>
            )}

            {fieldErrors.unattached.length > 0 && (
              <div role="alert">
                {fieldErrors.unattached.map((message) => (
                  <p key={message} className="t-small t-bad">
                    {message}
                  </p>
                ))}
              </div>
            )}

            <button
              className="btn btn-accent"
              type="submit"
              disabled={status === "saving"}
              style={{ width: "100%", height: 42 }}
            >
              {status === "saving" ? t("auth.creating") : t("auth.createAdmin")}
            </button>

            <div className="between">
              <span className="t-meta t-muted">
                {t("auth.alreadyBootstrapped")}
              </span>
              <Link
                to="/login"
                className="btn btn-ghost btn-sm"
                style={{ textDecoration: "none" }}
              >
                {t("auth.backToSignIn")}
              </Link>
            </div>
          </form>
        </div>
      </main>
    </div>
  );
};

export default BootstrapPage;
