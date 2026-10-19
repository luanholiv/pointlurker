import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Modal from "./Modal";

describe("Modal", () => {
  it("renders when open=true", () => {
    render(
      <Modal open={true} onClose={vi.fn()} title="Add store">
        <div className="modal-body">Body</div>
      </Modal>,
    );
    expect(screen.getByText("Add store")).toBeInTheDocument();
    expect(screen.getByText("Body")).toBeInTheDocument();
  });

  it("hidden when open=false", () => {
    render(
      <Modal open={false} onClose={vi.fn()} title="Add store">
        <div className="modal-body">Body</div>
      </Modal>,
    );
    expect(screen.queryByText("Add store")).not.toBeInTheDocument();
  });

  it("calls onClose on Escape key", async () => {
    const onClose = vi.fn();
    render(
      <Modal open={true} onClose={onClose} title="Test">
        <div className="modal-body">Content</div>
      </Modal>,
    );
    await userEvent.keyboard("{Escape}");
    expect(onClose).toHaveBeenCalled();
  });

  it("renders eyebrow and description when provided", () => {
    render(
      <Modal
        open={true}
        onClose={vi.fn()}
        title="Title"
        eyebrow="01 · ADD STORE"
        description="Some description"
      >
        <div className="modal-body" />
      </Modal>,
    );
    expect(screen.getByText("01 · ADD STORE")).toBeInTheDocument();
    expect(screen.getByText("Some description")).toBeInTheDocument();
  });

  it("Modal.Footer renders children", () => {
    render(
      <Modal open={true} onClose={vi.fn()} title="T">
        <div className="modal-body" />
        <Modal.Footer>
          <button>Cancel</button>
        </Modal.Footer>
      </Modal>,
    );
    expect(screen.getByRole("button", { name: "Cancel" })).toBeInTheDocument();
  });

  it("renders role=dialog and aria-modal=true when open", () => {
    render(
      <Modal open={true} onClose={vi.fn()} title="Test">
        <div className="modal-body">Content</div>
      </Modal>,
    );
    const dialog = screen.getByRole("dialog");
    expect(dialog).toBeInTheDocument();
    expect(dialog).toHaveAttribute("aria-modal", "true");
  });
});
