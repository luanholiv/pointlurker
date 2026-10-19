import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import MissingLabelDialog from "./MissingLabelDialog";
import { setAccessToken } from "../auth/authStorage";

const storeId = "store-1";

describe("MissingLabelDialog", () => {
  beforeEach(() => {
    setAccessToken("test-token");
  });

  it("renders nothing when open is false", () => {
    const { container } = render(
      <MissingLabelDialog
        open={false}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username"]}
      />,
    );
    expect(container.firstChild).toBeNull();
  });

  it("renders an input per missing label", () => {
    render(
      <MissingLabelDialog
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username", "Steam ID"]}
      />,
    );
    expect(
      screen.getByTestId("missing-label-Twitch Username"),
    ).toBeInTheDocument();
    expect(screen.getByTestId("missing-label-Steam ID")).toBeInTheDocument();
  });

  it("posts filled values and calls onSaved", async () => {
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

    const onSaved = vi.fn();
    render(
      <MissingLabelDialog
        open={true}
        onClose={vi.fn()}
        onSaved={onSaved}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username"]}
      />,
    );

    await userEvent.type(screen.getByLabelText("Twitch Username"), "xqc");
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() => expect(onSaved).toHaveBeenCalled());
    expect(putBody!.values).toEqual([
      { labelCanonical: "Twitch Username", value: "xqc" },
    ]);
  });

  it("calls onClose when Skip clicked", async () => {
    const onClose = vi.fn();
    render(
      <MissingLabelDialog
        open={true}
        onClose={onClose}
        onSaved={vi.fn()}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username"]}
      />,
    );
    await userEvent.click(
      screen.getByRole("button", { name: /Skip for now/i }),
    );
    expect(onClose).toHaveBeenCalled();
  });

  it("shows error on save failure", async () => {
    server.use(
      http.put(
        `http://localhost/api/stores/${storeId}/profile/values`,
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    const onSaved = vi.fn();
    render(
      <MissingLabelDialog
        open={true}
        onClose={vi.fn()}
        onSaved={onSaved}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username"]}
      />,
    );
    await userEvent.type(screen.getByLabelText("Twitch Username"), "v");
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );
    await waitFor(() => expect(onSaved).not.toHaveBeenCalled());
  });

  // A 500 with no problem body produces `Error("Request failed with status 500")`. That string is
  // for a developer reading a console, so the dialog shows its fallback key instead.
  it("shows the fallback catalog message when the response carries no code", async () => {
    server.use(
      http.put(
        `http://localhost/api/stores/${storeId}/profile/values`,
        () => new HttpResponse(null, { status: 500 }),
      ),
    );
    render(
      <MissingLabelDialog
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username"]}
      />,
    );
    await userEvent.type(screen.getByLabelText("Twitch Username"), "v");
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    expect(await screen.findByText("Failed to save")).toBeInTheDocument();
  });

  it("renders the catalog message for the backend code when one is returned", async () => {
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
      <MissingLabelDialog
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username"]}
      />,
    );
    await userEvent.type(screen.getByLabelText("Twitch Username"), "v");
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    expect(
      await screen.findByText("Value is too long (maximum 1024 characters)."),
    ).toBeInTheDocument();
  });

  // "Value is too long" only says which row it means when it sits under that row's input.
  it("attaches the message to the input the failing element came from", async () => {
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
      <MissingLabelDialog
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username"]}
      />,
    );
    await userEvent.type(screen.getByLabelText("Twitch Username"), "v");
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    const input = await screen.findByLabelText("Twitch Username");
    await waitFor(() =>
      expect(input).toHaveAccessibleDescription(
        "Value is too long (maximum 1024 characters).",
      ),
    );
    expect(input).toHaveAttribute("aria-invalid", "true");
  });

  // The index is into the array as submitted, and only rows the user filled in are sent — so the
  // second entry here is the second *submitted* row, not the second row on screen.
  it("attaches each entry to its own row when two of them break", async () => {
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
                {
                  field: "values[1].value",
                  code: "validation.required",
                  params: { field: "values[1].value" },
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
      <MissingLabelDialog
        open={true}
        onClose={vi.fn()}
        onSaved={vi.fn()}
        storeId={storeId}
        storeName="Test Store"
        missingLabels={["Twitch Username", "Discord Tag"]}
      />,
    );
    await userEvent.type(screen.getByLabelText("Twitch Username"), "v");
    await userEvent.type(screen.getByLabelText("Discord Tag"), "d");
    await userEvent.click(
      screen.getByRole("button", { name: /Save profile/i }),
    );

    await waitFor(() =>
      expect(
        screen.getByLabelText("Twitch Username"),
      ).toHaveAccessibleDescription(
        "Value is too long (maximum 1024 characters).",
      ),
    );
    expect(screen.getByLabelText("Discord Tag")).toHaveAccessibleDescription(
      "This field is required.",
    );
  });
});
