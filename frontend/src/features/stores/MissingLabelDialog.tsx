import { useState } from "react";
import { Check } from "lucide-react";
import { useTranslation } from "react-i18next";
import Modal from "../../components/ui/Modal";
import FieldError from "../../components/ui/FieldError";
import { fieldErrorProps } from "../../components/ui/fieldErrorProps";
import { tApiError } from "../../i18n/errorCatalog";
import { fieldIndex } from "../../i18n/fieldLabels";
import { useFieldErrors } from "../../hooks/useFieldErrors";
import { upsertStoreProfileValues } from "./storesApi";

type MissingLabelDialogProps = {
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
  storeId: string;
  storeName: string;
  missingLabels: string[];
};

const MissingLabelDialog = ({
  open,
  onClose,
  onSaved,
  storeId,
  storeName,
  missingLabels,
}: MissingLabelDialogProps) => {
  const { t } = useTranslation();
  const [values, setValues] = useState<Record<string, string>>({});
  const [status, setStatus] = useState<"idle" | "saving" | "error">("idle");
  const [error, setError] = useState("");
  const fieldErrors = useFieldErrors();

  const handleSubmit = async () => {
    setStatus("saving");
    setError("");
    fieldErrors.clear();
    const entries = Object.entries(values)
      .filter(([, v]) => v.trim().length > 0)
      .map(([labelCanonical, value]) => ({ labelCanonical, value }));
    try {
      if (entries.length > 0) {
        await upsertStoreProfileValues(storeId, entries);
      }
      setStatus("idle");
      setValues({});
      onSaved();
    } catch (err) {
      setStatus("error");
      // A collection rule names the element it broke by its position in the array as *submitted*,
      // and only the rows the user filled in are sent — so the index resolves through `entries`
      // rather than through the labels on screen.
      const labelAt = (field: string) => {
        const index = fieldIndex(field);
        return index === null ? null : (entries[index]?.labelCanonical ?? null);
      };
      if (
        !fieldErrors.setFromError(err, {
          anchor: labelAt,
          map: {},
          t,
          override: labelAt,
        })
      ) {
        setError(tApiError(err, "stores.missingLabels.saveFailed"));
      }
    }
  };

  if (!open) return null;
  return (
    <Modal
      open={true}
      onClose={onClose}
      title={
        <>
          {t("stores.missingLabels.title")}{" "}
          <em style={{ fontStyle: "italic", color: "var(--accent)" }}>
            {t("stores.missingLabels.titleEm")}
          </em>
        </>
      }
      description={t("stores.missingLabels.description", {
        storeName,
        count: missingLabels.length,
      })}
    >
      <div className="modal-body">
        <div className="vstack" style={{ gap: 10 }}>
          {missingLabels.map((label, index) => (
            <div
              key={label}
              className="field"
              data-testid={`missing-label-${label}`}
            >
              <label htmlFor={`missing-input-${label}`}>{label}</label>
              <input
                id={`missing-input-${label}`}
                className="input"
                type="text"
                value={values[label] ?? ""}
                onChange={(e) =>
                  setValues((prev) => ({ ...prev, [label]: e.target.value }))
                }
                {...fieldErrorProps(
                  `missing-error-${index}`,
                  fieldErrors.byField[label],
                )}
              />
              <FieldError
                id={`missing-error-${index}`}
                messages={fieldErrors.byField[label]}
              />
            </div>
          ))}
        </div>

        {status === "error" && error !== "" && (
          <p className="t-small t-bad">{error}</p>
        )}

        {fieldErrors.unattached.length > 0 && (
          <div role="alert">
            {fieldErrors.unattached.map((message) => (
              <p key={message} className="t-small t-bad">
                {message}
              </p>
            ))}
          </div>
        )}
      </div>

      <Modal.Footer>
        <button className="btn" type="button" onClick={onClose}>
          {t("stores.missingLabels.skip")}
        </button>
        <button
          className="btn btn-accent"
          type="button"
          disabled={status === "saving"}
          onClick={handleSubmit}
        >
          <Check size={13} aria-hidden={true} focusable="false" />
          {status === "saving"
            ? t("common.saving")
            : t("stores.missingLabels.saveProfile")}
        </button>
      </Modal.Footer>
    </Modal>
  );
};

export default MissingLabelDialog;
