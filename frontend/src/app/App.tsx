import { BrowserRouter } from "react-router";
import { ToastProvider } from "../components/ui";
import { AuthProvider } from "../features/auth/AuthProvider";
import AppRoutes from "../routes/AppRoutes";

const App = () => (
  <AuthProvider>
    {/* Location updates commit synchronously so a navigation started inside a View Transition
        renders the new page before the browser snapshots it. */}
    <BrowserRouter useTransitions={false}>
      {/* Inside the router so login and bootstrap can raise a toast too, and above the routes
          so the stack survives navigation instead of unmounting with the page that raised it. */}
      <ToastProvider>
        <AppRoutes />
      </ToastProvider>
    </BrowserRouter>
  </AuthProvider>
);

export default App;
