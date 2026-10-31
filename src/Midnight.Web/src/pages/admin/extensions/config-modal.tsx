import { extensionsApi } from "@/api/extensions";
import type { ExtensionDetail, ExtensionSummary } from "@/api/types";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { GearIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";

interface ConfigModalProps {
  extension: ExtensionSummary | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function ConfigModal({ extension, open, onOpenChange }: ConfigModalProps) {
  const { t } = useT();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);
  const [mode, setMode] = useState<"visual" | "json">("visual");
  const [configJson, setConfigJson] = useState("{}");
  const [parsedValues, setParsedValues] = useState<Record<string, unknown>>({});
  const [jsonError, setJsonError] = useState<string | null>(null);

  const { data: detail, isLoading } = useQuery<ExtensionDetail>({
    queryKey: ["admin", "extensions", extension?.id],
    queryFn: () => extensionsApi.get(extension!.id),
    enabled: open && !!extension?.id,
  });

  useEffect(() => {
    if (detail?.configValuesJson) {
      try {
        const parsed = JSON.parse(detail.configValuesJson);
        const pretty = JSON.stringify(parsed, null, 2);
        setConfigJson(pretty);
        setParsedValues(parsed);
        setJsonError(null);
      } catch {
        setConfigJson(detail.configValuesJson);
        setParsedValues({});
      }
    } else {
      setConfigJson("{}");
      setParsedValues({});
    }
  }, [detail]);

  const handleJsonChange = (val: string) => {
    setConfigJson(val);
    try {
      const parsed = JSON.parse(val);
      setParsedValues(parsed);
      setJsonError(null);
    } catch (e: unknown) {
      setJsonError(e instanceof Error ? e.message : "Invalid JSON format");
    }
  };

  const handleValueChange = (key: string, value: unknown) => {
    const updated = { ...parsedValues, [key]: value };
    setParsedValues(updated);
    setConfigJson(JSON.stringify(updated, null, 2));
    setJsonError(null);
  };

  const handleSave = async () => {
    if (!extension) return;
    try {
      // Validate JSON
      const parsed = JSON.parse(configJson);
      setBusy(true);
      await extensionsApi.updateConfig(extension.id, JSON.stringify(parsed));
      await queryClient.invalidateQueries({ queryKey: ["admin", "extensions"] });
      notifySuccess(t("Extensions.SaveSuccess"));
      onOpenChange(false);
    } catch (err: unknown) {
      if (err instanceof SyntaxError) {
        setJsonError("Invalid JSON format. Please check the syntax.");
      } else {
        notifyError(err);
      }
    } finally {
      setBusy(false);
    }
  };

  const configKeys = useMemo(() => Object.keys(parsedValues), [parsedValues]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <div className="flex items-center gap-2">
            <div className="flex size-9 items-center justify-center rounded-lg bg-brand-500/10 text-brand-400">
              <GearIcon className="size-5" />
            </div>
            <div>
              <DialogTitle>
                {extension?.name} · {t("Extensions.Settings")}
              </DialogTitle>
              <DialogDescription>
                Adjust the parameters and settings for this extension.
              </DialogDescription>
            </div>
          </div>
        </DialogHeader>

        {isLoading ? (
          <div className="py-12 text-center text-sm text-gray-400">Loading configuration…</div>
        ) : (
          <div className="space-y-4 py-2">
            <div className="flex items-center justify-between border-b border-gray-100 pb-3 dark:border-white/5">
              <span className="text-xs font-medium text-gray-500 dark:text-gray-400">
                Identifier: <code className="text-brand-400">{extension?.identifier}</code> · Version:{" "}
                <code className="text-brand-400">v{extension?.version}</code>
              </span>
              <div className="flex items-center rounded-lg border border-gray-200 p-0.5 text-xs dark:border-gray-800">
                <button
                  type="button"
                  onClick={() => setMode("visual")}
                  className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                    mode === "visual"
                      ? "bg-brand-500 text-white"
                      : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
                  }`}
                >
                  Form
                </button>
                <button
                  type="button"
                  onClick={() => setMode("json")}
                  className={`rounded-md px-2.5 py-1 font-medium transition-colors ${
                    mode === "json"
                      ? "bg-brand-500 text-white"
                      : "text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
                  }`}
                >
                  Raw JSON
                </button>
              </div>
            </div>

            {mode === "visual" ? (
              <div className="max-h-[380px] space-y-3.5 overflow-y-auto pr-1">
                {configKeys.length === 0 ? (
                  <div className="rounded-xl border border-dashed border-gray-200 p-8 text-center text-sm text-gray-500 dark:border-gray-800 dark:text-gray-400">
                    No keys configured. Switch to "Raw JSON" to add configuration parameters.
                  </div>
                ) : (
                  configKeys.map((key) => {
                    const value = parsedValues[key];
                    const isBool = typeof value === "boolean";
                    const isNum = typeof value === "number";

                    if (isBool) {
                      return (
                        <div
                          key={key}
                          className="flex items-center justify-between rounded-xl border border-gray-100 bg-gray-50/50 p-3.5 dark:border-white/5 dark:bg-white/2"
                        >
                          <div>
                            <Label className="text-sm font-medium text-gray-800 dark:text-white/90">
                              {key}
                            </Label>
                            <span className="block text-xs text-gray-500 dark:text-gray-400">
                              Toggle for {key}
                            </span>
                          </div>
                          <Switch
                            checked={Boolean(value)}
                            onCheckedChange={(checked) => handleValueChange(key, checked)}
                          />
                        </div>
                      );
                    }

                    return (
                      <div key={key} className="space-y-1.5">
                        <Label className="text-xs font-semibold uppercase tracking-wider text-gray-600 dark:text-gray-300">
                          {key}
                        </Label>
                        <Input
                          type={isNum ? "number" : "text"}
                          value={value !== null && value !== undefined ? String(value) : ""}
                          onChange={(e) =>
                            handleValueChange(
                              key,
                              isNum ? Number(e.target.value) : e.target.value,
                            )
                          }
                          placeholder={`Value for ${key}`}
                          className="font-mono text-sm"
                        />
                      </div>
                    );
                  })
                )}
              </div>
            ) : (
              <div className="space-y-2">
                <Textarea
                  value={configJson}
                  onChange={(e) => handleJsonChange(e.target.value)}
                  className="h-64 font-mono text-xs leading-relaxed"
                  placeholder='{\n  "key": "value"\n}'
                  spellCheck={false}
                />
                {jsonError && (
                  <p className="text-xs font-medium text-red-500 dark:text-red-400">{jsonError}</p>
                )}
              </div>
            )}
          </div>
        )}

        <DialogFooter className="gap-2 sm:gap-0">
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            {t("Common.Cancel")}
          </Button>
          <Button onClick={handleSave} disabled={busy || !!jsonError}>
            {busy ? "Saving…" : t("Common.Save")}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
