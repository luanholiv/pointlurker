import { describe, it, expect, beforeEach, vi } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import StoreProfileEditor from "./StoreProfileEditor";
import { setAccessToken } from "../auth/authStorage";
import type {
  StoreInputProfile,
  StoreProfileRequester,
  StoreProfileResolvedInput,
  StoreProfileUnresolvedInput,
} from "./storesApi";

const storeId = "store-1";

const makeProfile = (
  over: Partial<StoreInputProfile> = {},
): StoreInputProfile => ({
  unresolved: [],
  resolved: [],
  canonicalLabels: [],
  ...over,
});

const unresolvedInput = (
  over: Partial<StoreProfileUnresolvedInput> = {},
): StoreProfileUnresolvedInput => ({
  rawLabel: "raw label",
  bestCanonical: null,
  bestScore: 0,
  aliasTarget: null,
  itemCount: 1,
  blockedEntryCount: 0,
  ...over,
});

const resolvedInput = (
  over: Partial<StoreProfileResolvedInput> = {},
): StoreProfileResolvedInput => ({
  labelCanonical: "Twitch Username",
  updatedAt: "2026-04-01T00:00:00Z",
  requestedBy: [],
  ...over,
});

const requester = (
  over: Partial<StoreProfileRequester> = {},
): StoreProfileRequester => ({
  rawLabel: "Twitch Username",
  source: "Exact",
  confidence: 100,
  itemCount: 1,
  ...over,
});

describe("StoreProfileEditor", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  it("shows empty-state when the store has no input labels", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile()}
        onSaved={() => {}}
      />,
    );

    expect(screen.getByTestId("store-profile-editor")).toBeInTheDocument();
    expect(
      screen.getByRole("heading", { name: "No input labels" }),
    ).toBeInTheDocument();
  });

  it("renders every input in the same card shell whatever its state", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [unresolvedInput({ rawLabel: "Nome no jogo" })],
          resolved: [
            resolvedInput({ requestedBy: [requester()] }),
            resolvedInput({ labelCanonical: "Steam ID" }),
          ],
          canonicalLabels: ["Twitch Username", "Steam ID"],
        })}
        onSaved={() => {}}
      />,
    );

    for (const label of ["Nome no jogo", "Twitch Username", "Steam ID"]) {
      expect(screen.getByTestId(`profile-row-${label}`)).toHaveClass("card");
    }
  });

  it("gives each row one status pill with the tone matching its state", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [unresolvedInput({ rawLabel: "Nome no jogo" })],
          resolved: [
            resolvedInput({ requestedBy: [requester()] }),
            resolvedInput({ labelCanonical: "Steam ID" }),
          ],
          canonicalLabels: ["Twitch Username", "Steam ID"],
        })}
        onSaved={() => {}}
      />,
    );

    const needsValue = screen.getByTestId("profile-status-Nome no jogo");
    expect(needsValue).toHaveTextContent(/needs value/i);
    expect(needsValue).toHaveClass("pill", "warn");

    const resolved = screen.getByTestId("profile-status-Twitch Username");
    expect(resolved).toHaveTextContent(/^resolved$/i);
    expect(resolved).toHaveClass("pill", "ok");

    const unrequested = screen.getByTestId("profile-status-Steam ID");
    expect(unrequested).toHaveTextContent(/no item requests this/i);
    expect(unrequested).toHaveClass("pill");
    expect(unrequested).not.toHaveClass("ok", "warn");
  });

  it("offers both alternatives on every unresolved row, with or without a best match", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({
              rawLabel: "with match",
              bestCanonical: "Steam ID",
              bestScore: 72,
            }),
            unresolvedInput({ rawLabel: "without match" }),
          ],
          resolved: [resolvedInput({ labelCanonical: "Steam ID" })],
          canonicalLabels: ["Steam ID"],
        })}
        onSaved={() => {}}
      />,
    );

    for (const label of ["with match", "without match"]) {
      const row = screen.getByTestId(`profile-row-${label}`);
      expect(
        within(row).getByTestId(`profile-mode-value-${label}`),
      ).toBeEnabled();
      expect(
        within(row).getByTestId(`profile-mode-alias-${label}`),
      ).toBeEnabled();
    }
  });

  it("disables map-to-existing only when the store has no canonical labels", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [unresolvedInput({ rawLabel: "orphan" })],
        })}
        onSaved={() => {}}
      />,
    );

    expect(screen.getByTestId("profile-mode-alias-orphan")).toBeDisabled();
  });

  it("prefills the canonical picker with the server's best match when switching to alias mode", async () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({
              rawLabel: "steamid64",
              bestCanonical: "Steam ID",
              bestScore: 72,
            }),
          ],
          canonicalLabels: ["Steam ID"],
        })}
        onSaved={() => {}}
      />,
    );

    expect(screen.getByTestId("profile-bestmatch-steamid64")).toHaveTextContent(
      /best match: Steam ID \(72%\)/i,
    );
    await userEvent.click(screen.getByTestId("profile-mode-alias-steamid64"));

    expect(screen.getByTestId("profile-canonical-steamid64")).toHaveValue(
      "Steam ID",
    );
  });

  it("explains an alias that points at a canonical with no value", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({
              rawLabel: "Steam profile",
              aliasTarget: "Steam ID",
            }),
          ],
        })}
        onSaved={() => {}}
      />,
    );

    expect(
      screen.getByTestId("profile-aliastarget-Steam profile"),
    ).toHaveTextContent(/Steam ID.*no value yet/i);
  });

  it("shows live impact counts on an unresolved row", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({
              rawLabel: "Nome no jogo",
              itemCount: 3,
              blockedEntryCount: 2,
            }),
          ],
        })}
        onSaved={() => {}}
      />,
    );

    const meta = screen.getByTestId("profile-meta-Nome no jogo");
    expect(meta).toHaveTextContent(/requested by 3 items/i);
    expect(meta).toHaveTextContent(/blocks 2 auto-redeem entries/i);
  });

  it("tags each requester chip with the source the backend reported", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [
            resolvedInput({
              requestedBy: [
                requester({ rawLabel: "Twitch Username", source: "Exact" }),
                requester({ rawLabel: "twitchh usrname", source: "Alias" }),
                requester({
                  rawLabel: "Username Twitch",
                  source: "Fuzzy",
                  confidence: 91,
                }),
              ],
            }),
          ],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={() => {}}
      />,
    );

    expect(
      screen.getByTestId("profile-requester-Twitch Username"),
    ).toHaveTextContent(/exact/i);
    expect(
      screen.getByTestId("profile-requester-twitchh usrname"),
    ).toHaveTextContent(/alias/i);
    expect(
      screen.getByTestId("profile-requester-Username Twitch"),
    ).toHaveTextContent(/fuzzy 91%/i);
  });

  it("exposes a detach action on alias chips only", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [
            resolvedInput({
              requestedBy: [
                requester({ rawLabel: "Twitch Username", source: "Exact" }),
                requester({ rawLabel: "twitchh usrname", source: "Alias" }),
                requester({
                  rawLabel: "Username Twitch",
                  source: "Fuzzy",
                  confidence: 91,
                }),
              ],
            }),
          ],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={() => {}}
      />,
    );

    expect(
      screen.getByTestId("profile-detach-twitchh usrname"),
    ).toBeInTheDocument();
    expect(
      screen.queryByTestId("profile-detach-Twitch Username"),
    ).not.toBeInTheDocument();
    expect(
      screen.queryByTestId("profile-detach-Username Twitch"),
    ).not.toBeInTheDocument();
  });

  it("detaching an alias calls DELETE /profile/aliases and reloads", async () => {
    const deleted: string[] = [];
    server.use(
      http.delete(
        `http://localhost/api/stores/${storeId}/profile/aliases`,
        ({ request }) => {
          deleted.push(new URL(request.url).searchParams.get("rawLabel") ?? "");
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );
    const onSaved = vi.fn();

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [
            resolvedInput({
              requestedBy: [
                requester({ rawLabel: "twitchh usrname", source: "Alias" }),
              ],
            }),
          ],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={onSaved}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-detach-twitchh usrname"));

    await waitFor(() => expect(deleted).toEqual(["twitchh usrname"]));
    expect(onSaved).toHaveBeenCalledTimes(1);
  });

  it("delete hedges the outcome and names the requester count", async () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [
            resolvedInput({
              requestedBy: [
                requester({ rawLabel: "a" }),
                requester({ rawLabel: "b", source: "Alias" }),
                requester({ rawLabel: "c", source: "Fuzzy", confidence: 90 }),
              ],
            }),
          ],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={() => {}}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-delete-Twitch Username"));

    expect(
      screen.getByTestId("profile-delete-warning-Twitch Username"),
    ).toHaveTextContent(/3 labels resolve here.*may leave them unresolved/i);
  });

  it("shows no impact warning when nothing requests the slot", async () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [resolvedInput({ labelCanonical: "Steam ID" })],
          canonicalLabels: ["Steam ID"],
        })}
        onSaved={() => {}}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-delete-Steam ID"));

    expect(
      screen.getByTestId("profile-delete-confirm-Steam ID"),
    ).toBeInTheDocument();
    expect(
      screen.queryByTestId("profile-delete-warning-Steam ID"),
    ).not.toBeInTheDocument();
  });

  it("confirming delete calls DELETE /profile/values with the label and reloads", async () => {
    const deleted: string[] = [];
    server.use(
      http.delete(
        `http://localhost/api/stores/${storeId}/profile/values`,
        ({ request }) => {
          deleted.push(new URL(request.url).searchParams.get("label") ?? "");
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );
    const onSaved = vi.fn();

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [resolvedInput({ labelCanonical: "Discord tag / handle" })],
          canonicalLabels: ["Discord tag / handle"],
        })}
        onSaved={onSaved}
      />,
    );

    await userEvent.click(
      screen.getByTestId("profile-delete-Discord tag / handle"),
    );
    await userEvent.click(
      screen.getByTestId("profile-delete-confirm-Discord tag / handle"),
    );

    await waitFor(() => expect(deleted).toEqual(["Discord tag / handle"]));
    expect(onSaved).toHaveBeenCalledTimes(1);
  });

  it("single save fans out to one values PUT and one alias POST", async () => {
    let putBody: {
      values: { labelCanonical: string; value: string }[];
    } | null = null;
    const aliasPosts: { rawLabel: string; labelCanonical: string }[] = [];
    server.use(
      http.put(
        `http://localhost/api/stores/${storeId}/profile/values`,
        async ({ request }) => {
          putBody = (await request.json()) as typeof putBody;
          return new HttpResponse(null, { status: 204 });
        },
      ),
      http.post(
        `http://localhost/api/stores/${storeId}/profile/aliases`,
        async ({ request }) => {
          aliasPosts.push(
            (await request.json()) as (typeof aliasPosts)[number],
          );
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );
    const onSaved = vi.fn();

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({ rawLabel: "steamid64" }),
            unresolvedInput({
              rawLabel: "twitchx",
              bestCanonical: "Twitch Username",
              bestScore: 80,
            }),
          ],
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={onSaved}
      />,
    );

    await userEvent.type(
      screen.getByTestId("profile-value-steamid64"),
      "76561A",
    );
    await userEvent.click(screen.getByTestId("profile-mode-alias-twitchx"));
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody!.values).toEqual([
      { labelCanonical: "steamid64", value: "76561A" },
    ]);
    expect(aliasPosts).toEqual([
      { rawLabel: "twitchx", labelCanonical: "Twitch Username" },
    ]);
    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
  });

  it("replacing a stored value posts an upsert for its canonical label", async () => {
    let putBody: {
      values: { labelCanonical: string; value: string }[];
    } | null = null;
    server.use(
      http.put(
        `http://localhost/api/stores/${storeId}/profile/values`,
        async ({ request }) => {
          putBody = (await request.json()) as typeof putBody;
          return new HttpResponse(null, { status: 204 });
        },
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={() => {}}
      />,
    );

    await userEvent.type(
      screen.getByTestId("profile-input-Twitch Username"),
      "new-handle",
    );
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() => expect(putBody).not.toBeNull());
    expect(putBody!.values).toEqual([
      { labelCanonical: "Twitch Username", value: "new-handle" },
    ]);
  });

  it("reports why an operation failed and reloads the half that saved", async () => {
    server.use(
      http.put(
        `http://localhost/api/stores/${storeId}/profile/values`,
        () => new HttpResponse(null, { status: 204 }),
      ),
      http.post(
        `http://localhost/api/stores/${storeId}/profile/aliases`,
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    const onSaved = vi.fn();

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({ rawLabel: "discord" }),
            unresolvedInput({
              rawLabel: "twitchx",
              bestCanonical: "Twitch Username",
              bestScore: 80,
            }),
          ],
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={onSaved}
      />,
    );

    await userEvent.type(screen.getByTestId("profile-value-discord"), "disc#1");
    await userEvent.click(screen.getByTestId("profile-mode-alias-twitchx"));
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    expect(
      await screen.findByText("twitchx: Failed to save."),
    ).toBeInTheDocument();
    // The values PUT was accepted. Skipping the reload would leave its row rendered as unset
    // while the server holds the value.
    await waitFor(() => expect(onSaved).toHaveBeenCalledTimes(1));
    expect(screen.getByTestId("profile-value-discord")).toHaveValue("");
  });

  // A per-field rule has to reach its own row. Discarding the alias POST's rejection renders
  // "Failed to save: <label>" whatever the backend reported.
  it("attaches an alias rule to the row it came from", async () => {
    server.use(
      http.post(`http://localhost/api/stores/${storeId}/profile/aliases`, () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            code: "validation.failed",
            params: {
              errors: [
                {
                  field: "labelCanonical",
                  code: "validation.required",
                  params: { field: "labelCanonical" },
                },
              ],
            },
          },
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );
    const onSaved = vi.fn();

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({
              rawLabel: "twitchx",
              bestCanonical: "Twitch Username",
              bestScore: 80,
            }),
          ],
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={onSaved}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-mode-alias-twitchx"));
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() =>
      expect(
        screen.getByTestId("profile-canonical-twitchx"),
      ).toHaveAccessibleDescription("This field is required."),
    );
    expect(onSaved).not.toHaveBeenCalled();
  });

  it("attaches a values rule to the row whose value broke it", async () => {
    server.use(
      http.put(`http://localhost/api/stores/${storeId}/profile/values`, () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            code: "validation.failed",
            params: {
              errors: [
                {
                  field: "values[0].value",
                  code: "validation.too_long",
                  params: { field: "values[0].value", max: 1024 },
                },
              ],
            },
          },
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [unresolvedInput({ rawLabel: "discord" })],
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.type(screen.getByTestId("profile-value-discord"), "disc#1");
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() =>
      expect(
        screen.getByTestId("profile-value-discord"),
      ).toHaveAccessibleDescription(
        "Value is too long (maximum 1024 characters).",
      ),
    );
  });

  it("reports a vanished store once, not against a row that cannot fix it", async () => {
    server.use(
      http.post(`http://localhost/api/stores/${storeId}/profile/aliases`, () =>
        HttpResponse.json(
          {
            title: "Store not found.",
            status: 404,
            code: "stores.not_found",
            params: {},
          },
          {
            status: 404,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({
              rawLabel: "twitchx",
              bestCanonical: "Twitch Username",
              bestScore: 80,
            }),
          ],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-mode-alias-twitchx"));
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    expect(
      await screen.findByText("twitchx: Store not found."),
    ).toBeInTheDocument();
    expect(
      screen.getByTestId("profile-canonical-twitchx"),
    ).not.toHaveAccessibleDescription();
  });

  it("groups operations that failed for the same reason into one line", async () => {
    server.use(
      http.put(
        `http://localhost/api/stores/${storeId}/profile/values`,
        () => new HttpResponse(null, { status: 503 }),
      ),
      http.post(
        `http://localhost/api/stores/${storeId}/profile/aliases`,
        () => new HttpResponse(null, { status: 503 }),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({ rawLabel: "discord" }),
            unresolvedInput({
              rawLabel: "twitchx",
              bestCanonical: "Twitch Username",
              bestScore: 80,
            }),
          ],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.type(screen.getByTestId("profile-value-discord"), "disc#1");
    await userEvent.click(screen.getByTestId("profile-mode-alias-twitchx"));
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    expect(
      await screen.findByText("discord, twitchx: Failed to save."),
    ).toBeInTheDocument();
  });

  it("attaches an explained failure naming no field to every row it carried", async () => {
    server.use(
      http.put(`http://localhost/api/stores/${storeId}/profile/values`, () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            code: "validation.failed",
            params: { errors: [] },
          },
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({ rawLabel: "discord" }),
            unresolvedInput({ rawLabel: "steamid64" }),
          ],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.type(screen.getByTestId("profile-value-discord"), "disc#1");
    await userEvent.type(
      screen.getByTestId("profile-value-steamid64"),
      "76561A",
    );
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() =>
      expect(
        screen.getByTestId("profile-value-discord"),
      ).toHaveAccessibleDescription("One or more validation errors occurred."),
    );
    expect(
      screen.getByTestId("profile-value-steamid64"),
    ).toHaveAccessibleDescription("One or more validation errors occurred.");
    expect(screen.getByTestId("profile-value-discord")).toBeInvalid();
  });

  it("reports the reason a delete failed instead of a fixed sentence", async () => {
    server.use(
      http.delete(
        `http://localhost/api/stores/${storeId}/profile/values`,
        () => new HttpResponse(null, { status: 500 }),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-delete-Twitch Username"));
    await userEvent.click(
      screen.getByTestId("profile-delete-confirm-Twitch Username"),
    );

    expect(
      await screen.findByText("Twitch Username: Failed to delete."),
    ).toBeInTheDocument();
  });

  it("attaches an explained delete failure to the row it was aimed at", async () => {
    server.use(
      http.delete(`http://localhost/api/stores/${storeId}/profile/values`, () =>
        HttpResponse.json(
          {
            title: "One or more validation errors occurred.",
            status: 400,
            code: "validation.failed",
            params: {},
          },
          {
            status: 400,
            headers: { "Content-Type": "application/problem+json" },
          },
        ),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-delete-Twitch Username"));
    await userEvent.click(
      screen.getByTestId("profile-delete-confirm-Twitch Username"),
    );

    await waitFor(() =>
      expect(
        screen.getByTestId("profile-input-Twitch Username"),
      ).toHaveAccessibleDescription("One or more validation errors occurred."),
    );
  });

  // The chip carries the requester's raw label, which is not the key of the row holding it —
  // aiming the message at that label would render it nowhere.
  it("attaches a failed alias detach to the row that holds the chip", async () => {
    server.use(
      http.delete(
        `http://localhost/api/stores/${storeId}/profile/aliases`,
        () =>
          HttpResponse.json(
            {
              title: "One or more validation errors occurred.",
              status: 400,
              code: "validation.failed",
              params: {},
            },
            {
              status: 400,
              headers: { "Content-Type": "application/problem+json" },
            },
          ),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [
            resolvedInput({
              requestedBy: [
                requester({ rawLabel: "twitchx", source: "Alias" }),
              ],
            }),
          ],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.click(screen.getByTestId("profile-detach-twitchx"));

    await waitFor(() =>
      expect(
        screen.getByTestId("profile-input-Twitch Username"),
      ).toHaveAccessibleDescription("One or more validation errors occurred."),
    );
  });

  it("clears the messages a failed save left once a delete succeeds", async () => {
    server.use(
      http.put(
        `http://localhost/api/stores/${storeId}/profile/values`,
        () => new HttpResponse(null, { status: 500 }),
      ),
      http.delete(
        `http://localhost/api/stores/${storeId}/profile/values`,
        () => new HttpResponse(null, { status: 204 }),
      ),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          resolved: [resolvedInput({ requestedBy: [requester()] })],
          canonicalLabels: ["Twitch Username"],
        })}
        onSaved={vi.fn()}
      />,
    );

    await userEvent.type(
      screen.getByTestId("profile-input-Twitch Username"),
      "new-handle",
    );
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );
    expect(
      await screen.findByText("Twitch Username: Failed to save."),
    ).toBeInTheDocument();

    await userEvent.click(screen.getByTestId("profile-delete-Twitch Username"));
    await userEvent.click(
      screen.getByTestId("profile-delete-confirm-Twitch Username"),
    );

    await waitFor(() =>
      expect(
        screen.queryByText("Twitch Username: Failed to save."),
      ).not.toBeInTheDocument(),
    );
  });

  it("keeps the save button disabled until something is actionable", async () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [unresolvedInput({ rawLabel: "Twitch Username" })],
        })}
        onSaved={() => {}}
      />,
    );

    expect(
      screen.getByRole("button", { name: /Save profile/i }),
    ).toBeDisabled();
    await userEvent.type(
      screen.getByTestId("profile-value-Twitch Username"),
      "v",
    );
    expect(screen.getByRole("button", { name: /Save profile/i })).toBeEnabled();
  });

  it("renders the server's order verbatim", () => {
    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile({
          unresolved: [
            unresolvedInput({
              rawLabel: "zzz first from server",
              blockedEntryCount: 9,
            }),
            unresolvedInput({ rawLabel: "aaa second" }),
          ],
          resolved: [resolvedInput({ labelCanonical: "mmm last" })],
          canonicalLabels: ["mmm last"],
        })}
        onSaved={() => {}}
      />,
    );

    const rendered = screen
      .getAllByTestId(/^profile-row-/)
      .map((row) => row.getAttribute("data-testid"));
    expect(rendered).toEqual([
      "profile-row-zzz first from server",
      "profile-row-aaa second",
      "profile-row-mmm last",
    ]);
  });

  it("does not fetch the profile itself", () => {
    const requests: string[] = [];
    server.use(
      http.get(`http://localhost/api/stores/${storeId}/profile`, () => {
        requests.push("GET profile");
        return HttpResponse.json(makeProfile());
      }),
    );

    render(
      <StoreProfileEditor
        storeId={storeId}
        profile={makeProfile()}
        onSaved={() => {}}
      />,
    );

    expect(requests).toHaveLength(0);
  });
});
