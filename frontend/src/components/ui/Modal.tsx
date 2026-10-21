import * as Dialog from "@radix-ui/react-dialog";
import type { ReactNode } from "react";

interface ModalProps {
  open: boolean;
  onClose: () => void;
  title: ReactNode;
  description?: string;
  children: ReactNode;
}

const ModalFooter = ({ children }: { children: ReactNode }) => (
  <div className="modal-foot">{children}</div>
);

const Modal = ({ open, onClose, title, description, children }: ModalProps) => (
  <Dialog.Root open={open} onOpenChange={(o) => !o && onClose()}>
    <Dialog.Portal>
      <Dialog.Overlay className="backdrop" />
      <Dialog.Content
        className="modal"
        aria-modal={true}
        aria-describedby={description ? "modal-desc" : undefined}
      >
        <div className="modal-head">
          <Dialog.Title asChild>
            <h3>{title}</h3>
          </Dialog.Title>
          {description && (
            <Dialog.Description id="modal-desc">
              {description}
            </Dialog.Description>
          )}
        </div>
        {children}
      </Dialog.Content>
    </Dialog.Portal>
  </Dialog.Root>
);

Modal.Footer = ModalFooter;

export default Modal;
