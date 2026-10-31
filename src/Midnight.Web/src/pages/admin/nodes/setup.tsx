import { api } from "@/api/client";
import type { NodeSetup } from "@/api/types";
import { PageLoader } from "@/components/shared/guards";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { AlertIcon, CheckIcon, CopyIcon, RegenerateIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useNode } from "./layout";

function CodeBlock({ title, value }: { title: string; value: string }) {
  const { t } = useT();
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    await navigator.clipboard.writeText(value);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
      <div className="flex items-center justify-between border-b border-gray-100 px-5 py-3 dark:border-gray-800">
        <h3 className="text-sm font-semibold text-gray-800 dark:text-white/90">{title}</h3>
        <Button variant="outline" size="xs" onClick={copy}>
          {copied ? <CheckIcon className="text-green-500" /> : <CopyIcon />}
          {copied ? t("Common.Copied") : t("Common.Copy")}
        </Button>
      </div>
      <pre className="max-h-96 overflow-auto bg-gray-50 p-5 font-mono text-xs leading-relaxed text-gray-800 dark:bg-black/20 dark:text-gray-300">{value}</pre>
    </div>
  );
}

export default function NodeSetupPage() {
  const node = useNode();
  const { t } = useT();
  const confirm = useConfirm();
  const queryClient = useQueryClient();

  const setup = useQuery({ queryKey: ["admin", "nodes", node.id, "setup"], queryFn: () => api.get<NodeSetup>(`/api/admin/nodes/${node.id}/setup`), staleTime: 0 });

  const resetToken = async () => {
    if (!(await confirm({ title: t("Nodes.ResetToken"), message: t("Nodes.ResetTokenConfirm"), confirmLabel: t("Nodes.ResetToken"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.post(`/api/admin/nodes/${node.id}/reset-token`);
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes", node.id] });
      notifySuccess(t("Nodes.TokenReset"));
    } catch (error) {
      notifyError(error);
    }
  };

  if (!setup.data) return <PageLoader />;

  return (
    <div className="space-y-6">
      <Alert variant="warning" className="px-4 py-3">
        <AlertIcon />
        <AlertDescription>{t("Nodes.SetupSecret")}</AlertDescription>
      </Alert>

      <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
        <h3 className="text-lg font-semibold text-gray-800 dark:text-white/90">{t("Nodes.SetupTitle")}</h3>
        <ol className="mt-4 list-inside list-decimal space-y-2 text-sm text-gray-600 dark:text-gray-400">
          <li>{t("Nodes.SetupStep1")}</li>
          <li>{t("Nodes.SetupStep2")}</li>
          <li>{t("Nodes.SetupStep3")}</li>
        </ol>
      </div>

      <CodeBlock title={t("Nodes.SetupCommand")} value={setup.data.command} />
      <CodeBlock title="/etc/midnight/config.json" value={setup.data.configuration} />

      <div className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-red-200 bg-white p-5 dark:border-red-500/30 dark:bg-white/[0.02]">
        <div>
          <h4 className="font-semibold text-gray-800 dark:text-white/90">{t("Nodes.ResetToken")}</h4>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{t("Nodes.ResetTokenHint")}</p>
        </div>
        <Button variant="outline" className="text-red-500" onClick={resetToken}>
          <RegenerateIcon />
          {t("Nodes.ResetToken")}
        </Button>
      </div>
    </div>
  );
}
