import type { ReactNode } from "react";

interface KPIProps {
  label: string;
  value: ReactNode;
  delta?: string;
  deltaTone?: "ok" | "bad";
}

const KPI = ({ label, value, delta, deltaTone }: KPIProps) => (
  <div className="kpi">
    <div className="label">{label}</div>
    <div className="val">{value}</div>
    {delta && (
      <div className={`delta${deltaTone === "bad" ? " bad" : ""}`}>{delta}</div>
    )}
  </div>
);

export default KPI;
