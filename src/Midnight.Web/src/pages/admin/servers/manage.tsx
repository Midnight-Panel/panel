import { ApiError } from "@/api/client";
import { adminServersApi } from "@/api/servers";
import { SectionCard } from "@/components/shared/section-card";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { LockIcon, OctagonAlertIcon, RegenerateIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useNavigate } from "react-router";
import { adminServerQueryKey, useAdminServer } from "./hooks";

export default function AdminServerManagePage() {
  const server = useAdminServer();
  const { t } = useT();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState<"suspend" | "reinstall" | "delete" | null>(null);

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey: adminServerQueryKey(server.id) });
    await queryClient.invalidateQueries({ queryKey: ["admin", "servers", "list"] });
    await queryClient.invalidateQueries({ queryKey: ["servers"] });
  };

  const toggleSuspend = async () => {
    const suspend = !server.suspended;

    if (suspend && !(await confirm({
      title: t("Servers.Suspend"),
      message: t("Servers.SuspendConfirm", server.name),
      confirmLabel: t("Servers.Suspend"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy("suspend");

    try {
      if (suspend) await adminServersApi.suspend(server.id);
      else await adminServersApi.unsuspend(server.id);
      await refresh();
      notifySuccess(suspend ? t("Servers.Suspended", server.name) : t("Servers.Unsuspended", server.name));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const reinstall = async () => {
    if (!(await confirm({
      title: t("Servers.Reinstall"),
      message: t("Servers.ReinstallConfirm", server.name),
      confirmLabel: t("Servers.Reinstall"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy("reinstall");

    try {
      await adminServersApi.reinstall(server.id);
      await refresh();
      notifySuccess(t("Servers.ReinstallStarted"));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const remove = async () => {
    if (!(await confirm({
      title: t("Servers.Delete"),
      message: t("Servers.DeleteConfirm", server.name),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy("delete");

    const deleted = async () => {
      navigate("/admin/servers");
      notifySuccess(t("Servers.Deleted", server.name));
      await queryClient.invalidateQueries({ queryKey: ["admin", "servers"], refetchType: "none" });
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes"], refetchType: "none" });
    };

    try {
      await adminServersApi.delete(server.id);
      await deleted();
    } catch (error) {
      // The demon could not remove the container, offer to delete the server only in the panel
      if (error instanceof ApiError && error.status === 502) {
        const force = await confirm({
          title: t("Servers.ForceDelete"),
          message: t("Servers.ForceDeleteConfirm", error.message),
          confirmLabel: t("Servers.ForceDelete"),
          cancelLabel: t("Common.Cancel"),
        });

        if (force) {
          try {
            await adminServersApi.delete(server.id, true);
            await deleted();
          } catch (forceError) {
            notifyError(forceError);
          }
        }
      } else {
        notifyError(error);
      }
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
      <SectionCard
        icon={<LockIcon />}
        title={server.suspended ? t("Servers.Unsuspend") : t("Servers.Suspend")}
        description={server.suspended ? t("Servers.UnsuspendHint") : t("Servers.SuspendHint")}
        className="flex flex-col"
      >
        <div className="mt-auto">
          <Button variant={server.suspended ? "default" : "outline"} className="w-full" disabled={busy !== null} onClick={toggleSuspend}>
            <LockIcon />
            {server.suspended ? t("Servers.Unsuspend") : t("Servers.Suspend")}
          </Button>
        </div>
      </SectionCard>

      <SectionCard icon={<RegenerateIcon />} title={t("Servers.Reinstall")} description={t("Servers.ReinstallHint")} className="flex flex-col">
        <div className="mt-auto">
          <Button variant="outline" className="w-full" disabled={busy !== null} onClick={reinstall}>
            <RegenerateIcon />
            {t("Servers.Reinstall")}
          </Button>
        </div>
      </SectionCard>

      <SectionCard danger icon={<OctagonAlertIcon />} title={t("Users.DangerZone")} description={t("Servers.DeleteHint")} className="flex flex-col">
        <div className="mt-auto">
          <Button variant="destructive" className="w-full" disabled={busy !== null} onClick={remove}>
            <TrashBinIcon />
            {t("Servers.Delete")}
          </Button>
        </div>
      </SectionCard>
    </div>
  );
}
