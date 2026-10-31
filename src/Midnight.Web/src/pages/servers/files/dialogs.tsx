import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { notifyError } from "@/lib/notify";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMemo, useState, type ReactNode } from "react";
import { useForm } from "react-hook-form";
import * as z from "zod";
import { bitsToMode, isValidName, isValidRelativePath, modeToBits, normalizePath } from "./utils";

function DialogShell({ title, description, icon, children, onClose, className }: {
  title: string;
  description?: ReactNode;
  icon?: ReactNode;
  children: ReactNode;
  onClose: () => void;
  className?: string;
}) {
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className={className ?? "sm:max-w-md"}>
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2.5">
            {icon && (
              <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-brand-500/10 text-brand-500 dark:text-brand-400 [&>svg]:size-4">
                {icon}
              </span>
            )}
            {title}
          </DialogTitle>
          {description && <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        {children}
      </DialogContent>
    </Dialog>
  );
}

function Footer({ busy, submitLabel, onClose }: { busy: boolean; submitLabel: string; onClose: () => void }) {
  const { t } = useT();

  return (
    <DialogFooter className="mx-0 mb-0 p-0 pt-2">
      <Button type="button" variant="outline" onClick={onClose}>{t("Common.Cancel")}</Button>
      <Button type="submit" disabled={busy}>{submitLabel}</Button>
    </DialogFooter>
  );
}

/** Single text value: a name (no slashes), a relative path or an absolute directory */
export function TextValueDialog({ title, description, icon, label, hint, initial, placeholder, submitLabel, kind, onSubmit, onClose }: {
  title: string;
  description?: ReactNode;
  icon?: ReactNode;
  label: string;
  hint?: string;
  initial: string;
  placeholder?: string;
  submitLabel: string;
  kind: "name" | "path" | "directory";
  onSubmit: (value: string) => Promise<void>;
  onClose: () => void;
}) {
  const { t } = useT();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(() => {
    const value = z.string().trim().min(1, t("Validation.Required"));
    const refined =
      kind === "name"
        ? value.refine(isValidName, t("Files.Validation.Name"))
        : kind === "path"
          ? value.refine(isValidRelativePath, t("Files.Validation.Path"))
          : value.max(4096, t("Files.Validation.Path"));

    return z.object({ value: refined });
  }, [kind, t]);

  const form = useForm<{ value: string }>({ resolver: zodResolver(schema), defaultValues: { value: initial } });
  const error = form.formState.errors.value;

  const submit = async ({ value }: { value: string }) => {
    setBusy(true);

    try {
      await onSubmit(kind === "directory" ? normalizePath(value.trim()) : value.trim());
      onClose();
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  // Select the name without the extension, like file explorers do
  const selectName = (element: HTMLInputElement | null) => {
    if (!element || kind !== "name" || element.dataset.selected) return;
    element.dataset.selected = "1";
    const dot = element.value.lastIndexOf(".");
    requestAnimationFrame(() => element.setSelectionRange(0, dot > 0 ? dot : element.value.length));
  };

  const { ref, ...field } = form.register("value");

  return (
    <DialogShell title={title} description={description} icon={icon} onClose={onClose}>
      <form onSubmit={form.handleSubmit(submit)} noValidate className="space-y-4">
        <Field data-invalid={Boolean(error)}>
          <FieldLabel className="mb-1.5 block">{label}</FieldLabel>
          <Input
            autoFocus
            spellCheck={false}
            autoComplete="off"
            placeholder={placeholder}
            aria-invalid={error ? "true" : undefined}
            className={kind === "name" ? undefined : "font-mono"}
            ref={(element) => {
              ref(element);
              selectName(element);
            }}
            {...field}
          />
          {hint && !error && <FieldDescription>{hint}</FieldDescription>}
          <FieldError errors={[error]} />
        </Field>
        <Footer busy={busy} submitLabel={submitLabel} onClose={onClose} />
      </form>
    </DialogShell>
  );
}

/** rwx checkboxes for owner/group/others, synced with an octal input */
export function ChmodDialog({ fileName, initial, icon, onSubmit, onClose }: {
  fileName: string;
  initial: string;
  icon?: ReactNode;
  onSubmit: (mode: string) => Promise<void>;
  onClose: () => void;
}) {
  const { t } = useT();
  const [octal, setOctal] = useState(/^[0-7]{3,4}$/.test(initial) ? initial.slice(-3) : "644");
  const [busy, setBusy] = useState(false);
  const valid = /^[0-7]{3}$/.test(octal);
  const bits = modeToBits(valid ? octal : "000");

  const toggle = (who: number, bit: number, value: boolean) => {
    const next = bits.map((row) => [...row]);
    next[who][bit] = value;
    setOctal(bitsToMode(next));
  };

  const submit = async (event: React.FormEvent) => {
    event.preventDefault();
    if (!valid) return;

    setBusy(true);

    try {
      await onSubmit(octal);
      onClose();
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const groups = [t("Files.Chmod.Owner"), t("Files.Chmod.Group"), t("Files.Chmod.Others")];
  const rights = [t("Files.Chmod.Read"), t("Files.Chmod.Write"), t("Files.Chmod.Execute")];
  const symbolic = bits.map(([r, w, x]) => `${r ? "r" : "-"}${w ? "w" : "-"}${x ? "x" : "-"}`).join("");

  return (
    <DialogShell title={t("Files.Chmod.Title")} description={<span className="font-mono">{fileName}</span>} icon={icon} onClose={onClose}>
      <form onSubmit={submit} noValidate className="space-y-5">
        <div className="overflow-hidden rounded-xl border border-gray-200 dark:border-gray-800">
          <table className="w-full text-sm">
            <thead>
              <tr className="bg-gray-50 text-xs text-gray-500 dark:bg-white/[0.03] dark:text-gray-400">
                <th className="px-4 py-2.5 text-start font-medium" />
                {rights.map((right) => (
                  <th key={right} className="px-3 py-2.5 text-center font-medium">{right}</th>
                ))}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100 dark:divide-white/5">
              {groups.map((group, who) => (
                <tr key={group}>
                  <td className="px-4 py-3 font-medium text-gray-800 dark:text-white/90">{group}</td>
                  {rights.map((right, bit) => (
                    <td key={right} className="px-3 py-3">
                      <div className="flex justify-center">
                        <Checkbox
                          checked={bits[who][bit]}
                          onCheckedChange={(value) => toggle(who, bit, value)}
                          aria-label={`${group} ${right}`}
                        />
                      </div>
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="flex items-end gap-4">
          <Field data-invalid={!valid} className="w-32">
            <FieldLabel className="mb-1.5 block">{t("Files.Chmod.Octal")}</FieldLabel>
            <Input
              value={octal}
              maxLength={3}
              inputMode="numeric"
              className="font-mono"
              aria-invalid={valid ? undefined : "true"}
              onChange={(event) => setOctal(event.target.value.replace(/[^0-7]/g, "").slice(0, 3))}
            />
          </Field>
          <span className="mb-3 font-mono text-sm text-gray-500 dark:text-gray-400">{symbolic}</span>
        </div>
        {!valid && <p className="-mt-3 text-sm text-red-500">{t("Files.Validation.Mode")}</p>}

        <Footer busy={busy || !valid} submitLabel={t("Files.Chmod.Apply")} onClose={onClose} />
      </form>
    </DialogShell>
  );
}

/** Download a file from an url into the current directory (runs on the node in the background) */
export function PullDialog({ directory, icon, onSubmit, onClose }: {
  directory: string;
  icon?: ReactNode;
  onSubmit: (url: string, fileName: string | null) => Promise<void>;
  onClose: () => void;
}) {
  const { t } = useT();
  const [busy, setBusy] = useState(false);

  const schema = useMemo(
    () =>
      z.object({
        url: z
          .string()
          .trim()
          .min(1, t("Validation.Required"))
          .max(2048, t("Files.Validation.Url"))
          .refine((value) => {
            try {
              const parsed = new URL(value);
              return parsed.protocol === "http:" || parsed.protocol === "https:";
            } catch {
              return false;
            }
          }, t("Files.Validation.Url")),
        fileName: z.string().trim().refine((value) => !value || isValidName(value), t("Files.Validation.Name")),
      }),
    [t],
  );
  type Values = z.infer<typeof schema>;

  const form = useForm<Values>({ resolver: zodResolver(schema), defaultValues: { url: "", fileName: "" } });
  const { errors } = form.formState;

  const submit = async (values: Values) => {
    setBusy(true);

    try {
      await onSubmit(values.url.trim(), values.fileName.trim() || null);
      onClose();
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <DialogShell
      title={t("Files.Pull.Title")}
      description={t("Files.Pull.Description", directory)}
      icon={icon}
      onClose={onClose}
      className="sm:max-w-lg"
    >
      <form onSubmit={form.handleSubmit(submit)} noValidate className="space-y-4">
        <Field data-invalid={Boolean(errors.url)}>
          <FieldLabel className="mb-1.5 block">{t("Files.Pull.Url")}</FieldLabel>
          <Input autoFocus spellCheck={false} placeholder="https://" aria-invalid={errors.url ? "true" : undefined} {...form.register("url")} />
          <FieldError errors={[errors.url]} />
        </Field>
        <Field data-invalid={Boolean(errors.fileName)}>
          <FieldLabel className="mb-1.5 block">{t("Files.Pull.FileName")}</FieldLabel>
          <Input spellCheck={false} placeholder={t("Files.Pull.FileNamePlaceholder")} aria-invalid={errors.fileName ? "true" : undefined} {...form.register("fileName")} />
          {!errors.fileName && <FieldDescription>{t("Files.Pull.FileNameHint")}</FieldDescription>}
          <FieldError errors={[errors.fileName]} />
        </Field>
        <Footer busy={busy} submitLabel={t("Files.Pull.Submit")} onClose={onClose} />
      </form>
    </DialogShell>
  );
}
