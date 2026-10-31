import { Badge } from "@/components/ui/badge";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { LockIcon } from "@/icons";
import { isBooleanRule, isRequiredRule } from "@/lib/servers";
import type { VariableFormValue } from "../schema";

/** Inputs of shard / server variables with description, rules and defaults */
export function VariableFields({ variables, errors, onChange, disabled = false }: {
  variables: VariableFormValue[];
  errors: Array<string | undefined>;
  onChange: (index: number, value: string) => void;
  disabled?: boolean;
}) {
  const { t } = useT();

  if (variables.length === 0)
    return (
      <p className="rounded-xl border border-dashed border-gray-200 px-4 py-6 text-center text-sm text-gray-500 dark:border-gray-800 dark:text-gray-400">
        {t("Servers.NoVariables")}
      </p>
    );

  return (
    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
      {variables.map((variable, index) => {
        const error = errors[index];
        const readOnly = disabled || !variable.editable;
        const checked = ["true", "1"].includes(variable.value.toLowerCase());

        return (
          <div
            key={variable.envVariable}
            className="rounded-xl border border-gray-200 bg-gray-50/60 p-4 transition-colors hover:border-gray-300 dark:border-gray-800 dark:bg-white/[0.02] dark:hover:border-gray-700"
          >
            <Field data-invalid={Boolean(error)} className="gap-2">
              <div className="flex items-center justify-between gap-2">
                <FieldLabel className="flex items-center gap-1">
                  {variable.name}
                  {isRequiredRule(variable.rules) && <span className="text-red-500">*</span>}
                  {!variable.editable && <LockIcon className="size-3.5 text-gray-400" aria-label={t("Servers.ReadOnly")} />}
                </FieldLabel>
                <Badge size="sm" color="gray" className="font-mono text-[10px]">{variable.envVariable}</Badge>
              </div>

              {isBooleanRule(variable.rules) ? (
                <label className="flex cursor-pointer items-center gap-3 py-1.5">
                  <Switch
                    checked={checked}
                    disabled={readOnly}
                    onCheckedChange={(value) => onChange(index, value ? "true" : "false")}
                  />
                  <span className="font-mono text-xs text-gray-500 dark:text-gray-400">{checked ? "true" : "false"}</span>
                </label>
              ) : (
                <Input
                  value={variable.value}
                  disabled={readOnly}
                  placeholder={variable.defaultValue ?? ""}
                  aria-invalid={error ? "true" : undefined}
                  className="font-mono text-[13px]"
                  onChange={(event) => onChange(index, event.target.value)}
                />
              )}

              {variable.description && <FieldDescription className="text-xs">{variable.description}</FieldDescription>}
              <FieldError>{error}</FieldError>
              <p className="flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-gray-400 dark:text-gray-500">
                {variable.rules && <span className="font-mono">{variable.rules}</span>}
                {variable.defaultValue && <span>{t("Servers.DefaultValue", variable.defaultValue)}</span>}
              </p>
            </Field>
          </div>
        );
      })}
    </div>
  );
}
