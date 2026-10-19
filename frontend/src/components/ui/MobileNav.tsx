import * as Dialog from "@radix-ui/react-dialog";
import { Menu, X } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import Brand from "./Brand";
import NavList from "./NavList";

/**
 * The navigation below the sidebar breakpoint, where the sidebar is hidden. A Radix dialog, so
 * focus is trapped while it is open and returns to the menu button when it closes.
 */
const MobileNav = () => {
  const [open, setOpen] = useState(false);
  const { t } = useTranslation();

  return (
    <Dialog.Root open={open} onOpenChange={setOpen}>
      <Dialog.Trigger asChild>
        <button
          className="iconbtn menu-btn"
          aria-label={t("navigation.openMenu")}
        >
          <Menu size={15} aria-hidden={true} focusable="false" />
        </button>
      </Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="drawer-backdrop" />
        <Dialog.Content className="drawer" aria-describedby={undefined}>
          <div className="drawer-head">
            <Brand />
            <Dialog.Close asChild>
              <button
                className="iconbtn"
                aria-label={t("navigation.closeMenu")}
              >
                <X size={15} aria-hidden={true} focusable="false" />
              </button>
            </Dialog.Close>
          </div>
          <Dialog.Title className="sr-only">
            {t("navigation.menu")}
          </Dialog.Title>
          <NavList onNavigate={() => setOpen(false)} />
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  );
};

export default MobileNav;
