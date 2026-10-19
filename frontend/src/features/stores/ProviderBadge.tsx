import { useTranslation } from "react-i18next";
import { providerLabelKey, type StoreProvider } from "./storeProviders";

/** Which provider a store or a redemption belongs to, as a small mono label. */
const ProviderBadge = ({ provider }: { provider: StoreProvider }) => {
  const { t } = useTranslation();

  return (
    <span
      className="t-eyebrow"
      data-testid="provider-badge"
      style={{
        border: "1px solid var(--line-soft)",
        borderRadius: 3,
        padding: "1px 6px",
        whiteSpace: "nowrap",
      }}
    >
      {t(providerLabelKey(provider))}
    </span>
  );
};

export default ProviderBadge;
