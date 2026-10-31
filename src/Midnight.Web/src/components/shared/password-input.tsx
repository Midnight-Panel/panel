import { InputGroup, InputGroupAddon, InputGroupButton, InputGroupInput } from "@/components/ui/input-group";
import { EyeCloseIcon, EyeIcon } from "@/icons";
import { useState, type ComponentProps } from "react";

/** Password field with show/hide toggle (TailAdmin sign-in style) */
export function PasswordInput(props: ComponentProps<typeof InputGroupInput>) {
  const [visible, setVisible] = useState(false);

  return (
    <InputGroup>
      <InputGroupInput {...props} type={visible ? "text" : "password"} />
      <InputGroupAddon align="inline-end">
        <InputGroupButton
          size="icon-xs"
          onClick={() => setVisible((value) => !value)}
          aria-label={visible ? "Hide password" : "Show password"}
          className="text-gray-500 dark:text-gray-400"
        >
          {visible ? <EyeIcon className="fill-current" /> : <EyeCloseIcon className="fill-current" />}
        </InputGroupButton>
      </InputGroupAddon>
    </InputGroup>
  );
}
