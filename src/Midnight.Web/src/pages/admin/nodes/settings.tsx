import { api } from "@/api/client";
import type { NodeItem, NodeRequest } from "@/api/types";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { FloppyDiscIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router";
import { useNode } from "./layout";
import { NodeFormFields } from "./node-form";

export default function NodeSettingsPage() {
  const node = useNode();
  const { t } = useT();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const form = useForm<NodeRequest>({
    mode: "onTouched",
    defaultValues: { name: node.name, fqdn: node.fqdn, httpPort: node.httpPort, sftpPort: node.sftpPort, useSsl: node.useSsl },
  });

  const save = async (values: NodeRequest) => {
    setBusy(true);

    try {
      await api.put<NodeItem>(`/api/admin/nodes/${node.id}`, values);
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes"] });
      notifySuccess(t("Common.Saved"));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!(await confirm({ title: t("Nodes.Delete"), message: t("Nodes.DeleteConfirm", node.name), confirmLabel: t("Common.Delete"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.delete(`/api/admin/nodes/${node.id}`);
      // Leave first (stale lists reload on mount), refetching the open detail would only produce a 404
      navigate("/admin/nodes");
      notifySuccess(t("Nodes.Deleted"));
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes"], refetchType: "none" });
    } catch (error) {
      notifyError(error);
    }
  };

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
      <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:col-span-2 lg:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
        <h4 className="mb-6 text-lg font-semibold text-gray-800 dark:text-white/90">{t("Nodes.Settings")}</h4>
        <form onSubmit={form.handleSubmit(save)} className="space-y-5" noValidate>
          <NodeFormFields form={form} />
          <p className="text-xs text-gray-500 dark:text-gray-400">{t("Nodes.SettingsHint")}</p>
          <div className="flex justify-end">
            <Button type="submit" disabled={busy}>
              <FloppyDiscIcon />
              {t("Common.SaveChanges")}
            </Button>
          </div>
        </form>
      </div>

      <div className="rounded-2xl border border-red-200 bg-white p-5 lg:p-6 dark:border-red-500/30 dark:bg-white/[0.02]">
        <h4 className="mb-2 text-lg font-semibold text-red-600 dark:text-red-500">{t("Users.DangerZone")}</h4>
        <p className="mb-6 text-sm text-gray-500 dark:text-gray-400">{t("Nodes.DeleteHint")}</p>
        <Button variant="destructive" className="w-full" onClick={remove}>
          <TrashBinIcon />
          {t("Nodes.Delete")}
        </Button>
      </div>
    </div>
  );
}
