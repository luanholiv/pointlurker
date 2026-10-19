import { Route, Routes } from "react-router";
import AppLayout from "../app/AppLayout";
import RedemptionHistoryPage from "../features/redemptions/RedemptionHistoryPage";
import { AuthGate, BootstrapGate, LoginGate } from "./guards/AuthGate";
import BootstrapPage from "./pages/BootstrapPage";
import GetStartedPage from "./pages/GetStartedPage";
import LoginPage from "./pages/LoginPage";
import SettingsPage from "./pages/SettingsPage";
import StoreDetailPage from "./pages/StoreDetailPage";
import StoresPage from "./pages/StoresPage";
import WishlistPage from "./pages/WishlistPage";

const AppRoutes = () => (
  <Routes>
    <Route
      path="/bootstrap"
      element={
        <BootstrapGate>
          <BootstrapPage />
        </BootstrapGate>
      }
    />
    <Route
      path="/login"
      element={
        <LoginGate>
          <LoginPage />
        </LoginGate>
      }
    />
    <Route
      element={
        <AuthGate>
          <AppLayout />
        </AuthGate>
      }
    >
      <Route index element={<GetStartedPage />} />
      <Route path="/stores" element={<StoresPage />} />
      <Route path="/stores/:id" element={<StoreDetailPage />} />
      <Route path="/wishlist" element={<WishlistPage />} />
      <Route path="/redemptions" element={<RedemptionHistoryPage />} />
      <Route path="/settings" element={<SettingsPage />} />
    </Route>
  </Routes>
);

export default AppRoutes;
