import { useTranslation } from "react-i18next";

const Brand = () => {
  const { t } = useTranslation();
  return (
    <div className="brand">
      <img src="/icon.svg?v=2" className="brand-mark" alt="" />
      <div>
        <div className="brand-name">{t("common.brandName")}</div>
        <div className="brand-sub">{t("common.brandSub")}</div>
      </div>
    </div>
  );
};

export default Brand;
