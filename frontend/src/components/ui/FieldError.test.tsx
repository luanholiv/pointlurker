import { render, screen } from "@testing-library/react";
import FieldError from "./FieldError";
import { fieldErrorProps } from "./fieldErrorProps";

describe("FieldError", () => {
  it("renders nothing when there are no messages", () => {
    const { container } = render(<FieldError id="channel-id-error" />);
    expect(container).toBeEmptyDOMElement();
  });

  it("renders nothing for an empty list", () => {
    const { container } = render(
      <FieldError id="channel-id-error" messages={[]} />,
    );
    expect(container).toBeEmptyDOMElement();
  });

  it("renders the message under the id the input points at", () => {
    render(
      <FieldError
        id="channel-id-error"
        messages={["This field is required."]}
      />,
    );

    expect(screen.getByRole("alert")).toHaveAttribute("id", "channel-id-error");
    expect(screen.getByText("This field is required.")).toBeInTheDocument();
  });

  // Identity reports one entry per unmet password requirement, so several rules land on one input.
  it("renders every rule that broke on the same field", () => {
    render(
      <FieldError
        id="pw-error"
        messages={["Needs a digit.", "Needs an uppercase letter."]}
      />,
    );

    expect(screen.getByText("Needs a digit.")).toBeInTheDocument();
    expect(screen.getByText("Needs an uppercase letter.")).toBeInTheDocument();
  });

  it("announces itself, so a failure that moves no focus is still heard", () => {
    render(
      <FieldError
        id="channel-id-error"
        messages={["This field is required."]}
      />,
    );

    expect(screen.getByRole("alert")).toHaveTextContent(
      "This field is required.",
    );
  });
});

describe("fieldErrorProps", () => {
  it("marks the input invalid and points it at the message", () => {
    expect(
      fieldErrorProps("se-token-error", ["This field is required."]),
    ).toEqual({
      "aria-invalid": true,
      "aria-describedby": "se-token-error",
    });
  });

  // Both must be absent rather than false/empty: `aria-describedby` pointing at an element that is
  // not rendered is a dangling reference, and `aria-invalid="false"` is noise on a pristine field.
  it("leaves a field with no error untouched", () => {
    expect(fieldErrorProps("se-token-error", undefined)).toEqual({
      "aria-invalid": undefined,
      "aria-describedby": undefined,
    });
  });

  it("treats an empty list as no error", () => {
    expect(fieldErrorProps("se-token-error", [])).toEqual({
      "aria-invalid": undefined,
      "aria-describedby": undefined,
    });
  });
});
