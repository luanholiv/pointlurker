import { CheckCircle2, Circle, CircleDashed, PartyPopper } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Pill, TransitionLink } from "../../components/ui";
import type { SetupStep, SetupStepId } from "./useSetupProgress";
import SetupProgressBar from "./SetupProgressBar";

const STEP_COPY: Record<
  SetupStepId,
  { title: string; body: string; cta: string; to: string }
> = {
  credential: {
    title: "onboarding.steps.credential.title",
    body: "onboarding.steps.credential.body",
    cta: "onboarding.steps.credential.cta",
    to: "/settings#token",
  },
  store: {
    title: "onboarding.steps.store.title",
    body: "onboarding.steps.store.body",
    cta: "onboarding.steps.store.cta",
    to: "/stores",
  },
  wishlist: {
    title: "onboarding.steps.wishlist.title",
    body: "onboarding.steps.wishlist.body",
    cta: "onboarding.steps.wishlist.cta",
    to: "/stores",
  },
};

type StepState = "done" | "current" | "upcoming";

const stepState = (step: SetupStep, currentId?: SetupStepId): StepState => {
  if (step.done) return "done";
  return step.id === currentId ? "current" : "upcoming";
};

interface SetupChecklistProps {
  steps: SetupStep[];
  done: number;
  total: number;
  /** The optional step, ticked when any wishlist entry has auto-redeem on. */
  autoRedeemDone: boolean;
  /** Ends setup; offered only once every required step is done. */
  onFinish: () => void;
}

const optionalState = (isComplete: boolean, autoRedeemDone: boolean) => {
  if (autoRedeemDone) return "done";
  return isComplete ? "current" : "optional";
};

/**
 * The steps in the order they depend on each other. Only the first open step offers its action:
 * the ones after it cannot be done yet, and offering them would send the owner to an empty page.
 * The optional step joins in once the required ones are done, since it needs a wishlist entry.
 */
const SetupChecklist = ({
  steps,
  done,
  total,
  autoRedeemDone,
  onFinish,
}: SetupChecklistProps) => {
  const { t } = useTranslation();
  const currentId = steps.find((step) => !step.done)?.id;
  const isComplete = currentId === undefined;
  const autoRedeemState = optionalState(isComplete, autoRedeemDone);
  const AutoRedeemIcon = autoRedeemDone ? CheckCircle2 : CircleDashed;

  return (
    <div className="panel">
      {isComplete && (
        <div className="setup-complete">
          <PartyPopper
            className="setup-complete-icon"
            size={22}
            aria-hidden={true}
            focusable="false"
          />
          <div>
            <h2 className="t-heading">{t("onboarding.complete.title")}</h2>
            <p className="t-small setup-step-body">
              {t("onboarding.complete.body")}
            </p>
          </div>
          <button
            type="button"
            className="btn btn-accent setup-step-action"
            onClick={onFinish}
          >
            {t("onboarding.complete.finish")}
          </button>
        </div>
      )}

      <div className="vstack" style={{ gap: 8, marginBottom: 18 }}>
        <span className="t-meta t-muted">
          {t("onboarding.progress", { done, total })}
        </span>
        <SetupProgressBar done={done} total={total} />
      </div>

      <ol className="setup-steps stagger">
        {steps.map((step) => {
          const state = stepState(step, currentId);
          const Icon = step.done ? CheckCircle2 : Circle;
          const copy = STEP_COPY[step.id];

          return (
            <li key={step.id} className="setup-step" data-state={state}>
              <Icon
                className="setup-step-icon"
                size={18}
                aria-hidden={true}
                focusable="false"
              />
              <div>
                <h2 className="t-subheading setup-step-title">
                  {t(copy.title)}
                  {step.done && (
                    <span className="sr-only"> {t("onboarding.done")}</span>
                  )}
                </h2>
                <p className="t-small setup-step-body">{t(copy.body)}</p>
              </div>
              {state === "current" && (
                <TransitionLink
                  className="btn btn-accent setup-step-action"
                  to={copy.to}
                >
                  {t(copy.cta)}
                </TransitionLink>
              )}
            </li>
          );
        })}

        <li className="setup-step" data-state={autoRedeemState}>
          <AutoRedeemIcon
            className="setup-step-icon"
            size={18}
            aria-hidden={true}
            focusable="false"
          />
          <div>
            <h2 className="t-subheading setup-step-title">
              {t("onboarding.steps.autoRedeem.title")}{" "}
              <Pill tone="default">{t("onboarding.optional")}</Pill>
              {autoRedeemDone && (
                <span className="sr-only"> {t("onboarding.done")}</span>
              )}
            </h2>
            <p className="t-small setup-step-body">
              {t("onboarding.steps.autoRedeem.body")}
            </p>
          </div>
          {autoRedeemState === "current" && (
            <TransitionLink className="btn setup-step-action" to="/wishlist">
              {t("onboarding.steps.autoRedeem.cta")}
            </TransitionLink>
          )}
        </li>
      </ol>
    </div>
  );
};

export default SetupChecklist;
