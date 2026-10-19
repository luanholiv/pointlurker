import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router";
import { ApiError } from "../../lib/apiClient";
import AddStoreDialog from "./AddStoreDialog";

vi.mock("./useStores", () => ({
  useCreateStore: vi.fn(),
  useStoreLookup: vi.fn(),
}));

import { useStoreLookup, useCreateStore } from "./useStores";
const mockUseCreateStore = vi.mocked(useCreateStore);
const mockUseStoreLookup = vi.mocked(useStoreLookup);

const idleLookup = {
  state: { status: "idle" as const },
  lookup: vi.fn(),
  reset: vi.fn(),
};
const createIdle = {
  execute: vi.fn(),
  status: "idle" as const,
  errorMessage: "",
  error: null,
  reset: vi.fn(),
};
const resolvedLookup = {
  state: {
    status: "resolved" as const,
    provider: "streamelements" as const,
    externalId: "ch-1",
    displayName: "xQc",
    itemCount: null,
  },
  lookup: vi.fn(),
  reset: vi.fn(),
};

type Providers = "streamelements" | "gaupoints";

const renderDialog = (
  open: boolean,
  onClose = vi.fn(),
  onCreated = vi.fn(),
  {
    credentialProviders = ["streamelements", "gaupoints"],
    addedSingleStoreProviders = [],
  }: {
    credentialProviders?: Providers[];
    addedSingleStoreProviders?: Providers[];
  } = {},
) =>
  render(
    <MemoryRouter>
      <AddStoreDialog
        open={open}
        onClose={onClose}
        onCreated={onCreated}
        credentialProviders={credentialProviders}
        addedSingleStoreProviders={addedSingleStoreProviders}
      />
    </MemoryRouter>,
  );

describe("AddStoreDialog", () => {
  it("does not render when closed", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(false);
    expect(screen.queryByText(/Add store/i)).not.toBeInTheDocument();
  });

  it("channel username input and Lookup button visible on open", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true);
    expect(screen.getByText("Channel username")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Lookup" })).toBeInTheDocument();
  });

  it("Cancel button is visible and enabled on open", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true);
    const cancel = screen.getByRole("button", { name: "Cancel" });
    expect(cancel).toBeInTheDocument();
    expect(cancel).not.toBeDisabled();
  });

  it("Continue button is visible and disabled on open before lookup", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true);
    expect(screen.getByRole("button", { name: /Continue/i })).toBeDisabled();
  });

  it("Display name field never rendered — not before and not after lookup", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(idleLookup);
    const { rerender } = renderDialog(true);
    expect(
      screen.queryByPlaceholderText("e.g. My Favorite Streamer"),
    ).not.toBeInTheDocument();

    mockUseStoreLookup.mockReturnValue(resolvedLookup);
    rerender(
      <MemoryRouter>
        <AddStoreDialog
          open={true}
          onClose={vi.fn()}
          onCreated={vi.fn()}
          credentialProviders={["streamelements"]}
          addedSingleStoreProviders={[]}
        />
      </MemoryRouter>,
    );
    expect(
      screen.queryByPlaceholderText("e.g. My Favorite Streamer"),
    ).not.toBeInTheDocument();
  });

  it("resolved card shows display name from lookup", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(resolvedLookup);
    renderDialog(true);
    expect(screen.getByText("xQc")).toBeInTheDocument();
    expect(screen.getByText(/ch-1/)).toBeInTheDocument();
  });

  it("Continue enabled after lookup resolves", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(resolvedLookup);
    renderDialog(true);
    expect(
      screen.getByRole("button", { name: /Continue/i }),
    ).not.toBeDisabled();
  });

  it("Sync interval is not rendered in any state", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(resolvedLookup);
    renderDialog(true);
    expect(screen.queryByText(/Sync interval/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Every 4 minutes/i)).not.toBeInTheDocument();
  });

  it("Continue click calls createStore with channelId+displayName from lookup and calls onCreated", async () => {
    const onCreated = vi.fn();
    const executeFn = vi.fn().mockResolvedValue({ id: "store-1" });
    mockUseCreateStore.mockReturnValue({
      execute: executeFn,
      status: "idle" as const,
      errorMessage: "",
      error: null,
      reset: vi.fn(),
    });
    mockUseStoreLookup.mockReturnValue(resolvedLookup);
    renderDialog(true, vi.fn(), onCreated);
    await userEvent.click(screen.getByRole("button", { name: /Continue/i }));
    expect(executeFn).toHaveBeenCalledWith({
      provider: "streamelements",
      channelId: "ch-1",
      displayName: "xQc",
    });
    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });

  it("create error clears when modal is closed and reopened", async () => {
    const resetFn = vi.fn();
    mockUseCreateStore.mockReturnValue({
      execute: vi.fn(),
      status: "error" as const,
      errorMessage: "Something went wrong",
      error: null,
      reset: resetFn,
    });
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true);
    expect(screen.getByText("Something went wrong")).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(resetFn).toHaveBeenCalled();
  });

  // Neither validated field has an input here — both come from the channel lookup — so the only
  // way the user can tell which rules broke is for each sentence to name its own field.
  it("names every field when one submission breaks two of them", () => {
    mockUseCreateStore.mockReturnValue({
      execute: vi.fn(),
      status: "error" as const,
      errorMessage: "This field is required.",
      error: new ApiError({
        code: "validation.failed",
        params: {
          errors: [
            { field: "channelId", code: "validation.required" },
            { field: "displayName", code: "validation.required" },
          ],
        },
        status: 400,
        title: "One or more validation errors occurred.",
      }),
      reset: vi.fn(),
    });
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true);

    expect(
      screen.getByText("Channel ID: This field is required."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Store name: This field is required."),
    ).toBeInTheDocument();
  });

  it("falls back to the plain message when the failure reports no fields", () => {
    mockUseCreateStore.mockReturnValue({
      execute: vi.fn(),
      status: "error" as const,
      errorMessage: "Failed to add store",
      error: new ApiError({
        code: "stores.already_exists",
        status: 409,
        title: "Conflict",
      }),
      reset: vi.fn(),
    });
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true);

    expect(screen.getByText("Failed to add store")).toBeInTheDocument();
  });

  it("shows lookup error and keeps Continue disabled on lookup failure", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue({
      state: {
        status: "error",
        provider: "streamelements",
        message: "No channel found for 'xyz'.",
      },
      lookup: vi.fn(),
      reset: vi.fn(),
    });
    renderDialog(true);
    expect(screen.getByText("No channel found for 'xyz'.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Continue/i })).toBeDisabled();
  });

  it("shows loading state during lookup", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue({
      state: { status: "loading", provider: "streamelements" },
      lookup: vi.fn(),
      reset: vi.fn(),
    });
    renderDialog(true);
    expect(
      screen.getByRole("button", { name: "Looking up..." }),
    ).toBeDisabled();
  });

  it("calls lookup with trimmed username when Lookup clicked", async () => {
    const lookupFn = vi.fn();
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue({
      state: { status: "idle" },
      lookup: lookupFn,
      reset: vi.fn(),
    });
    renderDialog(true);
    await userEvent.type(screen.getByPlaceholderText("e.g. xqc"), "xqc");
    await userEvent.click(screen.getByRole("button", { name: "Lookup" }));
    expect(lookupFn).toHaveBeenCalledWith("streamelements", "xqc");
  });

  it("resets lookup when username edited after resolution", async () => {
    const resetFn = vi.fn();
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue({
      ...resolvedLookup,
      reset: resetFn,
    });
    renderDialog(true);
    await userEvent.type(screen.getByPlaceholderText("e.g. xqc"), "a");
    expect(resetFn).toHaveBeenCalled();
  });

  it("closes on Escape key", async () => {
    const onClose = vi.fn();
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true, onClose);
    await userEvent.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalled();
  });

  it("calls onClose when Cancel clicked", async () => {
    const onClose = vi.fn();
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(idleLookup);
    renderDialog(true, onClose);
    await userEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onClose).toHaveBeenCalled();
  });

  it("resolved card shows the channel id when the lookup carries no item count", () => {
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue(resolvedLookup);
    renderDialog(true);
    expect(screen.getByText("id · ch-1")).toBeInTheDocument();
  });

  it("picking GauPoints hides the username field and looks the store up at once", async () => {
    const lookupFn = vi.fn();
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue({ ...idleLookup, lookup: lookupFn });
    renderDialog(true);

    await userEvent.click(screen.getByRole("button", { name: "GauPoints" }));

    expect(lookupFn).toHaveBeenCalledWith("gaupoints");
    expect(screen.queryByText("Channel username")).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "GauPoints" })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
  });

  it("adds a GauPoints store by provider alone, with its item count shown", async () => {
    const executeFn = vi.fn().mockResolvedValue({
      id: "store-2",
      displayName: "GauPoints",
      missingLabels: [],
    });
    const lookupFn = vi.fn();
    mockUseCreateStore.mockReturnValue({ ...createIdle, execute: executeFn });
    mockUseStoreLookup.mockReturnValue({ ...idleLookup, lookup: lookupFn });
    const { rerender } = renderDialog(true);
    await userEvent.click(screen.getByRole("button", { name: "GauPoints" }));

    mockUseStoreLookup.mockReturnValue({
      state: {
        status: "resolved",
        provider: "gaupoints",
        externalId: "gaupoints",
        displayName: "GauPoints",
        itemCount: 12,
      },
      lookup: lookupFn,
      reset: vi.fn(),
    });
    rerender(
      <MemoryRouter>
        <AddStoreDialog
          open={true}
          onClose={vi.fn()}
          onCreated={vi.fn()}
          credentialProviders={["streamelements", "gaupoints"]}
          addedSingleStoreProviders={[]}
        />
      </MemoryRouter>,
    );

    expect(screen.getByText("12 items")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /Continue/i }));
    expect(executeFn).toHaveBeenCalledWith({ provider: "gaupoints" });
  });

  it("says which credential is missing and skips the lookup without it", async () => {
    const lookupFn = vi.fn();
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue({ ...idleLookup, lookup: lookupFn });
    renderDialog(true, vi.fn(), vi.fn(), {
      credentialProviders: ["streamelements"],
    });

    await userEvent.click(screen.getByRole("button", { name: "GauPoints" }));

    expect(
      screen.getByText("Add your GauPoints credential in Settings first."),
    ).toBeInTheDocument();
    expect(lookupFn).not.toHaveBeenCalled();
    expect(screen.getByRole("button", { name: /Continue/i })).toBeDisabled();
  });

  it("says a single-store provider is already watched and skips the lookup", async () => {
    const lookupFn = vi.fn();
    mockUseCreateStore.mockReturnValue(createIdle);
    mockUseStoreLookup.mockReturnValue({ ...idleLookup, lookup: lookupFn });
    renderDialog(true, vi.fn(), vi.fn(), {
      addedSingleStoreProviders: ["gaupoints"],
    });

    await userEvent.click(screen.getByRole("button", { name: "GauPoints" }));

    expect(
      screen.getByText("GauPoints is already being watched."),
    ).toBeInTheDocument();
    expect(lookupFn).not.toHaveBeenCalled();
  });
});
