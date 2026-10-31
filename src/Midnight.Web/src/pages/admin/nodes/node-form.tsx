import type { NodeRequest } from "@/api/types";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import type { UseFormReturn } from "react-hook-form";

export const defaultNodeValues: NodeRequest = { name: "", fqdn: "", httpPort: 8080, sftpPort: 2022, useSsl: false };

/** Shared fields of the create dialog and the settings tab */
export function NodeFormFields({ form, disabled = false }: { form: UseFormReturn<NodeRequest>; disabled?: boolean }) {
  const { t } = useT();
  const { register, watch, setValue, formState: { errors } } = form;

  const portRules = {
    valueAsNumber: true,
    min: { value: 1, message: t("Nodes.PortRange") },
    max: { value: 65535, message: t("Nodes.PortRange") },
    required: t("Nodes.PortRange"),
  } as const;

  return (
    <fieldset disabled={disabled} className="space-y-5">
      <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
        <Field data-invalid={Boolean(errors.name)}>
          <FieldLabel className="mb-1.5 block">{t("Common.Name")}</FieldLabel>
          <Input placeholder="node-01" aria-invalid={errors.name ? "true" : undefined}
                 {...register("name", { required: t("Validation.Required"), maxLength: { value: 64, message: t("Validation.Required") } })} />
          <FieldError errors={[errors.name]} />
        </Field>
        <Field data-invalid={Boolean(errors.fqdn)}>
          <FieldLabel className="mb-1.5 block">{t("Nodes.Fqdn")}</FieldLabel>
          <Input placeholder="node01.example.com" aria-invalid={errors.fqdn ? "true" : undefined}
                 {...register("fqdn", { required: t("Nodes.FqdnInvalid"), pattern: { value: /^([a-zA-Z0-9]([a-zA-Z0-9.-]*[a-zA-Z0-9])?|\[?[0-9a-fA-F:]+\]?)$/, message: t("Nodes.FqdnInvalid") } })} />
          <FieldDescription>{t("Nodes.FqdnHint")}</FieldDescription>
          <FieldError errors={[errors.fqdn]} />
        </Field>
        <Field data-invalid={Boolean(errors.httpPort)}>
          <FieldLabel className="mb-1.5 block">{t("Nodes.HttpPort")}</FieldLabel>
          <Input type="number" aria-invalid={errors.httpPort ? "true" : undefined} {...register("httpPort", portRules)} />
          <FieldError errors={[errors.httpPort]} />
        </Field>
        <Field data-invalid={Boolean(errors.sftpPort)}>
          <FieldLabel className="mb-1.5 block">{t("Nodes.SftpPort")}</FieldLabel>
          <Input type="number" aria-invalid={errors.sftpPort ? "true" : undefined} {...register("sftpPort", portRules)} />
          <FieldError errors={[errors.sftpPort]} />
        </Field>
      </div>
      <label className="flex cursor-pointer items-start gap-3">
        <Switch checked={watch("useSsl") ?? false} onCheckedChange={(checked) => setValue("useSsl", Boolean(checked), { shouldDirty: true })} disabled={disabled} />
        <span>
          <span className="block text-sm font-medium text-gray-700 dark:text-gray-300">{t("Nodes.UseSsl")}</span>
          <span className="block text-xs text-gray-500 dark:text-gray-400">{t("Nodes.UseSslHint")}</span>
        </span>
      </label>
    </fieldset>
  );
}
