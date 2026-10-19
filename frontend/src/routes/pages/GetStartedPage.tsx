import { Navigate, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import { PageHead } from "../../components/ui";
import SetupChecklist from "../../features/onboarding/SetupChecklist";
import { useSetupDismissed } from "../../features/onboarding/setupDismissal";
import { useSetupProgress } from "../../features/onboarding/useSetupProgress";
import { withViewTransition } from "../../lib/viewTransition";

/**
 * The landing page until the owner finishes setup. A completed checklist stays here — it is where
 * the optional step is offered — until it is finished; after that, or when progress cannot be
 * read, the stores page is home.
 */
const GetStartedPage = () => {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const progress = useSetupProgress();
  const [dismissed, dismiss] = useSetupDismissed();

  if (dismissed || progress.status === "error") {
    return <Navigate to="/stores" replace />;
  }
  if (progress.status === "loading") return null;

  const finish = () =>
    void withViewTransition(() => {
      dismiss();
      navigate("/stores", { replace: true });
    });

  return (
    <div className="page">
      <PageHead
        title={t("onboarding.title")}
        description={t("onboarding.description")}
      />
      <SetupChecklist
        steps={progress.steps}
        done={progress.done}
        total={progress.total}
        autoRedeemDone={progress.autoRedeem}
        onFinish={finish}
      />
    </div>
  );
};

export default GetStartedPage;
