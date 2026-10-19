import { Navigate } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "../../features/auth/AuthContext";
import type { JSX } from "react";

type GateProps = {
  children: JSX.Element;
};

const LoadingScreen = () => {
  const { t } = useTranslation();
  return (
    <div className="flex min-h-[60vh] items-center justify-center text-sm text-text-muted">
      {t("auth.loadingStatus")}
    </div>
  );
};

export const AuthGate = ({ children }: GateProps) => {
  const { bootstrapStatus, bootstrapState, isAuthenticated, isUserLoading } =
    useAuth();

  if (bootstrapStatus === "loading") {
    return <LoadingScreen />;
  }

  if (isUserLoading) {
    return <LoadingScreen />;
  }

  if (!bootstrapState?.userExists) {
    return <Navigate to="/bootstrap" replace />;
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />;
  }

  return children;
};

export const BootstrapGate = ({ children }: GateProps) => {
  const { bootstrapStatus, bootstrapState } = useAuth();

  if (bootstrapStatus === "loading") {
    return <LoadingScreen />;
  }

  if (bootstrapState?.userExists) {
    return <Navigate to="/login" replace />;
  }

  return children;
};

export const LoginGate = ({ children }: GateProps) => {
  const { bootstrapStatus, bootstrapState, isAuthenticated } = useAuth();

  if (bootstrapStatus === "loading") {
    return <LoadingScreen />;
  }

  if (!bootstrapState?.userExists) {
    return <Navigate to="/bootstrap" replace />;
  }

  if (isAuthenticated) {
    return <Navigate to="/" replace />;
  }

  return children;
};
