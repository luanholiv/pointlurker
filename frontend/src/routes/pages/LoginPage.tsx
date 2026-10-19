import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../features/auth/AuthContext";
import { tApiError } from "../../i18n/errorCatalog";

const LoginPage = () => {
  const navigate = useNavigate();
  const { login } = useAuth();
  const { t } = useTranslation();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [twoFactorCode, setTwoFactorCode] = useState("");
  const [requiresTwoFactor, setRequiresTwoFactor] = useState(false);
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");
  const [errorMessage, setErrorMessage] = useState("");

  const handleSubmit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setStatus("saving");
    setErrorMessage("");
    try {
      const result = await login({
        email,
        password,
        twoFactorCode: requiresTwoFactor ? twoFactorCode : null,
      });
      if (result.requiresTwoFactor) {
        setRequiresTwoFactor(true);
        setStatus("idle");
        return;
      }
      navigate("/", { replace: true });
    } catch (error) {
      setStatus("error");
      setErrorMessage(tApiError(error, "auth.signInFailed"));
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

          <h1 className="t-hero" style={{ marginBottom: 6 }}>
            {t("auth.loginButton")}
          </h1>
          <p className="t-muted" style={{ margin: "0 0 22px" }}>
            {t("auth.signInDescription")}
          </p>

          <form className="vstack" style={{ gap: 14 }} onSubmit={handleSubmit}>
            <div className="field">
              <label htmlFor="login-email">{t("auth.email")}</label>
              <input
                id="login-email"
                className="input"
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
              />
            </div>
            <div className="field">
              <label htmlFor="login-password">{t("auth.password")}</label>
              <input
                id="login-password"
                className="input"
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>

            {requiresTwoFactor && (
              <div className="field">
                <label htmlFor="login-2fa">{t("auth.authenticatorCode")}</label>
                <input
                  id="login-2fa"
                  className="input"
                  type="text"
                  value={twoFactorCode}
                  onChange={(e) => setTwoFactorCode(e.target.value)}
                  inputMode="numeric"
                  required
                />
              </div>
            )}

            {status === "error" && (
              <p role="alert" className="t-small t-bad">
                {errorMessage}
              </p>
            )}

            <button
              className="btn btn-accent"
              type="submit"
              disabled={status === "saving"}
              style={{ width: "100%", height: 42 }}
            >
              {status === "saving"
                ? t("auth.signingIn")
                : t("auth.loginButton")}
              {status !== "saving" && (
                <span className="kbd" style={{ marginLeft: 6 }}>
                  ↵
                </span>
              )}
            </button>
          </form>

          <div className="divider" />
          <div className="between">
            <span />
            <Link
              to="/bootstrap"
              className="btn btn-ghost btn-sm"
              style={{ textDecoration: "none" }}
            >
              {t("auth.bootstrapLink")}
            </Link>
          </div>
        </div>
      </main>
    </div>
  );
};

export default LoginPage;
