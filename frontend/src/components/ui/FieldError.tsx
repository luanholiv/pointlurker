interface FieldErrorProps {
  /** The id the described input points at. Absent from the DOM while there is nothing to say. */
  id: string;
  messages?: readonly string[];
}

/**
 * The failure messages for one input, rendered beneath it.
 *
 * A backend rule reports which field it broke, and the sentences it resolves to are field-agnostic
 * on purpose — "This field is required." only says which field when it sits under that field's
 * label. That is what this is for.
 *
 * Several rules can break on one field at once, which is the normal case for a password, so this
 * takes a list. They share one container, so the input's accessible description is all of them.
 */
const FieldError = ({ id, messages }: FieldErrorProps) =>
  messages === undefined || messages.length === 0 ? null : (
    <div id={id} className="field-error" role="alert">
      {messages.map((message) => (
        <p key={message}>{message}</p>
      ))}
    </div>
  );

export default FieldError;
