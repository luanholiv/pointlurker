import { render } from "@testing-library/react";
import type { ReactElement } from "react";
import { MemoryRouter } from "react-router";
import ToastProvider from "../components/ui/ToastProvider";

interface RenderWithToastsOptions {
  /**
   * Wrap the tree in a `MemoryRouter` entered at this path. Omit it when the UI under test
   * builds its own router (a `Routes`/`Route` tree needs `initialEntries` the caller controls).
   */
  route?: string;
}

/**
 * Renders under the app's toast host.
 *
 * `useToast` throws without a provider, and in the real app `App` supplies one for every route.
 * A component test that renders a page directly has to supply the same thing, so this is the
 * single place that knows how — rather than every suite growing its own wrapper.
 */
export const renderWithToasts = (
  ui: ReactElement,
  { route }: RenderWithToastsOptions = {},
) =>
  render(
    <ToastProvider>
      {route === undefined ? (
        ui
      ) : (
        <MemoryRouter initialEntries={[route]}>{ui}</MemoryRouter>
      )}
    </ToastProvider>,
  );
