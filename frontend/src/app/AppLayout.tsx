import { useEffect } from "react";
import { Outlet, useLocation } from "react-router";
import { Sidebar, Topbar } from "../components/ui";
import SetupProgressWidget from "../features/onboarding/SetupProgressWidget";

const AppLayout = () => {
  const location = useLocation();

  useEffect(() => {
    document.getElementById("main-content")?.focus();
  }, [location.pathname]);

  return (
    <div className="app">
      <Sidebar footer={<SetupProgressWidget />} />
      <main id="main-content" tabIndex={-1}>
        <Topbar />
        <Outlet />
      </main>
    </div>
  );
};

export default AppLayout;
