import type { ReactNode } from "react";

interface PageHeadProps {
  title: ReactNode;
  description?: string;
  right?: ReactNode;
}

const PageHead = ({ title, description, right }: PageHeadProps) => (
  <div className="page-head">
    <div>
      <h1>{title}</h1>
      {description && <p>{description}</p>}
    </div>
    {right && <div className="hstack">{right}</div>}
  </div>
);

export default PageHead;
