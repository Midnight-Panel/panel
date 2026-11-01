import type { CreatedApplicationKey } from "@/api/application-keys";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { FieldLabel } from "@/components/ui/field";
import { InputGroup, InputGroupAddon, InputGroupButton, InputGroupInput } from "@/components/ui/input-group";
import { useT } from "@/i18n";
import { AlertIcon, CheckIcon, CopyIcon } from "@/icons";
import { notifyError } from "@/lib/notify";
import { useState } from "react";

function useCopy() {
  const [copied, setCopied] = useState<string | null>(null);

  const copy = async (id: string, value: string) => {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(id);
      window.setTimeout(() => setCopied((current) => (current === id ? null : current)), 2000);
    } catch (error) {
      notifyError(error);
    }
  };

  return { copied, copy };
}

/** Shows the full token exactly once after creating a key */
export function TokenDialog({ created, onClose }: { created: CreatedApplicationKey; onClose: () => void }) {
  const { t } = useT();
  const { copied, copy } = useCopy();
  const curl = `curl -H "Authorization: Bearer ${created.token}" ${window.location.origin}/api/application/v1/servers`;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="sm:max-w-2xl">
        <DialogHeader>
          <DialogTitle>{t("ApplicationApi.Token.Title")}</DialogTitle>
          <DialogDescription>{created.key.description}</DialogDescription>
        </DialogHeader>

        <div className="space-y-5">
          <Alert variant="warning" className="px-4 py-3">
            <AlertIcon />
            <AlertTitle>{t("ApplicationApi.Token.WarningTitle")}</AlertTitle>
            <AlertDescription>{t("ApplicationApi.Token.Warning")}</AlertDescription>
          </Alert>

          <div>
            <FieldLabel className="mb-1.5 block">{t("ApplicationApi.Token.Token")}</FieldLabel>
            <InputGroup>
              <InputGroupInput readOnly value={created.token} className="font-mono text-xs" onFocus={(event) => event.currentTarget.select()} />
              <InputGroupAddon align="inline-end">
                <InputGroupButton size="icon-xs" onClick={() => copy("token", created.token)} aria-label={t("Common.Copy")} title={t("Common.Copy")}>
                  {copied === "token" ? <CheckIcon className="text-green-500" /> : <CopyIcon />}
                </InputGroupButton>
              </InputGroupAddon>
            </InputGroup>
          </div>

          <div>
            <div className="mb-1.5 flex items-center justify-between gap-2">
              <FieldLabel className="block">{t("ApplicationApi.Token.Example")}</FieldLabel>
              <Button type="button" variant="ghost" size="xs" onClick={() => copy("curl", curl)}>
                {copied === "curl" ? <CheckIcon className="size-3.5 text-green-500" /> : <CopyIcon className="size-3.5" />}
                {copied === "curl" ? t("ApplicationApi.Token.Copied") : t("Common.Copy")}
              </Button>
            </div>
            <pre className="overflow-x-auto rounded-xl border border-gray-200 bg-gray-50 p-3 font-mono text-xs leading-relaxed text-gray-700 dark:border-gray-800 dark:bg-white/[0.03] dark:text-gray-300">
              <code>{curl}</code>
            </pre>
          </div>

          <DialogFooter className="mx-0 mb-0 p-0">
            <Button onClick={onClose}>{t("ApplicationApi.Token.Done")}</Button>
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
