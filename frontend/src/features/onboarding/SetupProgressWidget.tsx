import { CheckCircle2 } from "lucide-react";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { TransitionLink, useToast } from "../../components/ui";
import SetupProgressBar from "./SetupProgressBar";
import { useSetupDismissed } from "./setupDismissal";
import { useSetupProgress } from "./useSetupProgress";

/**
 * The checklist's footprint in the sidebar, so setup stays one click away from any page until it
 * is finished. It is mounted on every page, so it is also what announces the last required step
 * being done, which usually happens away from the checklist.
 */
const SetupProgressWidget = () => {
  const { t } = useTranslation();
  const { showToast } = useToast();
  const progress = useSetupProgress();
  const [dismissed] = useSetupDismissed();
  const isComplete = progress.status === "ready" && progress.isComplete;
  const wasComplete = useRef<boolean | null>(null);

  useEffect(() => {
    if (progress.status !== "ready") return;
    // The first reading only sets the baseline: a setup already complete is not news.
    if (wasComplete.current === false && isComplete && !dismissed) {
      showToast({
        kicker: t("onboarding.toast.kicker"),
        msg: t("onboarding.toast.msg"),
      });
    }
    wasComplete.current = isComplete;
  }, [progress.status, isComplete, dismissed, showToast, t]);

  if (progress.status !== "ready" || dismissed) return null;

  return (
    <TransitionLink className="setup-widget" to="/">
      <span className="between">
        <span className="t-eyebrow">
          {isComplete
            ? t("onboarding.widget.complete")
            : t("onboarding.widget.label")}
        </span>
        {isComplete ? (
          <CheckCircle2
            className="setup-widget-check"
            size={14}
            aria-hidden={true}
            focusable="false"
          />
        ) : (
          <span className="t-meta">
            {t("onboarding.widget.count", {
              done: progress.done,
              total: progress.total,
            })}
          </span>
        )}
      </span>
      <SetupProgressBar done={progress.done} total={progress.total} />
    </TransitionLink>
  );
};

export default SetupProgressWidget;
