import * as RadixSwitch from "@radix-ui/react-switch";

interface SwitchProps {
  checked: boolean;
  onChange: () => void;
  label?: string;
}

const Switch = ({ checked, onChange, label }: SwitchProps) => (
  <RadixSwitch.Root
    className="switch"
    checked={checked}
    onCheckedChange={() => onChange()}
    aria-label={label}
  >
    <RadixSwitch.Thumb className="switch-thumb" />
  </RadixSwitch.Root>
);

export default Switch;
