import type { ReactNode } from "react";

export type TabDef = {
  id: string;
  label: string;
  indicator?: ReactNode;
};

interface TabsProps {
  tabs: TabDef[];
  activeId: string;
  onSelect: (id: string) => void;
}

const Tabs = ({ tabs, activeId, onSelect }: TabsProps) => (
  <div className="tabs" role="tablist">
    {tabs.map((tab) => (
      <button
        key={tab.id}
        type="button"
        role="tab"
        className={`tab${tab.id === activeId ? " active" : ""}`}
        aria-selected={tab.id === activeId}
        onClick={() => onSelect(tab.id)}
      >
        {tab.label}
        {tab.indicator}
      </button>
    ))}
  </div>
);

export default Tabs;
