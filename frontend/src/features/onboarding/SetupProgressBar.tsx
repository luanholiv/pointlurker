import { useTranslation } from "react-i18next";

interface SetupProgressBarProps {
  done: number;
  total: number;
}

const SetupProgressBar = ({ done, total }: SetupProgressBarProps) => {
  const { t } = useTranslation();

  return (
    <div
      className="setup-bar"
      role="progressbar"
      aria-label={t("onboarding.widget.progressLabel")}
      aria-valuemin={0}
      aria-valuemax={total}
      aria-valuenow={done}
    >
      <div
        className="setup-bar-fill"
        style={{ width: `${(done / total) * 100}%` }}
      />
    </div>
  );
};

export default SetupProgressBar;
