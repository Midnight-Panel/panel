import { serverAllocationsApi, serverFeatureKeys } from "@/api/server-features";
import type { ServerAllocation } from "@/api/types";
import { tableStyles } from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { useT } from "@/i18n";
import { CheckIcon, InfoCircleIcon, PencilIcon, PlusIcon, StarIcon, TrashBinIcon, XIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { allocationLabel } from "@/lib/servers";
import { cn } from "@/utils";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type KeyboardEvent } from "react";
import { CopyButton } from "../components/badges";
import { CardLoader, FeatureCard, ServerPermissionGate, UsagePill } from "../components/feature-ui";
import { serverQueryKey, useServerContext } from "../hooks";

function NotesCell({ allocation, editable, onSave }: {
  allocation: ServerAllocation;
  editable: boolean;
  onSave: (notes: string | null) => Promise<boolean>;
}) {
  const { t } = useT();
  const [draft, setDraft] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    if (draft === null) return;

    const value = draft.trim();

    if (value === (allocation.notes ?? "")) {
      setDraft(null);
      return;
    }

    setSaving(true);
    const saved = await onSave(value || null);
    setSaving(false);

    if (saved) setDraft(null);
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      void save();
    }

    if (event.key === "Escape") {
      event.preventDefault();
      setDraft(null);
    }
  };

  if (draft !== null)
    return (
      <div className="flex items-center gap-1">
        <Input
          autoFocus
          value={draft}
          maxLength={255}
          disabled={saving}
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={onKeyDown}
          placeholder={t("ServerNetwork.NotesPlaceholder")}
          className="h-8 min-w-40 text-sm"
          aria-label={t("ServerNetwork.Notes")}
        />
        <Button variant="ghost" size="icon-xs" disabled={saving} onClick={save} aria-label={t("Common.Save")}>
          {saving ? <Spinner className="size-4" /> : <CheckIcon className="size-4 text-green-500" />}
        </Button>
        <Button variant="ghost" size="icon-xs" disabled={saving} onClick={() => setDraft(null)} aria-label={t("Common.Cancel")}>
          <XIcon className="size-4" />
        </Button>
      </div>
    );

  return (
    <button
      type="button"
      disabled={!editable}
      onClick={() => setDraft(allocation.notes ?? "")}
      className={cn(
        "group inline-flex max-w-xs items-center gap-2 rounded-md text-start disabled:cursor-default",
        editable && "hover:text-gray-800 dark:hover:text-white/90",
      )}
    >
      <span className={cn("truncate", !allocation.notes && "text-gray-400 italic dark:text-gray-500")}>
        {allocation.notes || (editable ? t("ServerNetwork.AddNotes") : "-")}
      </span>
      {editable && <PencilIcon className="size-3.5 shrink-0 opacity-0 transition-opacity group-hover:opacity-100" />}
    </button>
  );
}

function AllocationList() {
  const { server, can } = useServerContext();
  const { t } = useT();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState<number | "create" | null>(null);
  const queryKey = serverFeatureKeys.allocations(server.uuid);

  const { data, isLoading } = useQuery({ queryKey, queryFn: () => serverAllocationsApi.list(server.uuid) });

  const allocations = [...(data?.allocations ?? [])].sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary) || a.port - b.port);
  const limit = data?.limit ?? server.featureLimits.allocations;
  const additional = allocations.filter((allocation) => !allocation.isPrimary).length;
  const canUpdate = can(ServerPermissions.AllocationUpdate) && !server.suspended;
  const canDelete = can(ServerPermissions.AllocationDelete) && !server.suspended;

  const refresh = async () => {
    await queryClient.invalidateQueries({ queryKey });
    // The header shows the primary allocation
    await queryClient.invalidateQueries({ queryKey: serverQueryKey(server.uuid), exact: true });
  };

  const create = async () => {
    setBusy("create");

    try {
      const created = await serverAllocationsApi.create(server.uuid);
      await refresh();
      notifySuccess(t("ServerNetwork.Added", allocationLabel(created)));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const saveNotes = async (allocation: ServerAllocation, notes: string | null) => {
    try {
      await serverAllocationsApi.updateNotes(server.uuid, allocation.id, notes);
      await queryClient.invalidateQueries({ queryKey });
      return true;
    } catch (error) {
      notifyError(error);
      return false;
    }
  };

  const makePrimary = async (allocation: ServerAllocation) => {
    setBusy(allocation.id);

    try {
      await serverAllocationsApi.makePrimary(server.uuid, allocation.id);
      await refresh();
      notifySuccess(t("ServerNetwork.PrimaryChanged", allocationLabel(allocation)));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const remove = async (allocation: ServerAllocation) => {
    if (!(await confirm({
      title: t("ServerNetwork.Remove"),
      message: t("ServerNetwork.RemoveConfirm", allocationLabel(allocation)),
      confirmLabel: t("Common.Remove"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy(allocation.id);

    try {
      await serverAllocationsApi.delete(server.uuid, allocation.id);
      await refresh();
      notifySuccess(t("ServerNetwork.Removed", allocationLabel(allocation)));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  return (
    <div className="space-y-4">
      <FeatureCard
        title={t("ServerNetwork.Title")}
        description={t("ServerNetwork.Description")}
        actions={
          <>
            <UsagePill used={additional} limit={limit} label={t("ServerNetwork.Additional")} />
            {can(ServerPermissions.AllocationCreate) && (
              <Button
                size="sm"
                disabled={busy !== null || additional >= limit || server.suspended}
                onClick={create}
                title={additional >= limit ? t("ServerNetwork.LimitReached") : undefined}
              >
                {busy === "create" ? <Spinner className="size-4" /> : <PlusIcon className="size-4" />}
                {t("ServerNetwork.Add")}
              </Button>
            )}
          </>
        }
      >
        {isLoading ? (
          <CardLoader />
        ) : (
          <div className="max-w-full overflow-x-auto">
            <Table>
              <TableHeader className="border-b border-gray-100 dark:border-white/5">
                <TableRow>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ServerNetwork.Address")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ServerNetwork.Notes")}</TableCell>
                  <TableCell isHeader className={cn(tableStyles.headerCell, "text-end")}>{""}</TableCell>
                </TableRow>
              </TableHeader>
              <TableBody className="divide-y divide-gray-100 dark:divide-white/5">
                {allocations.map((allocation) => (
                  <TableRow key={allocation.id}>
                    <TableCell className={tableStyles.bodyCell}>
                      <span className="inline-flex items-center gap-2">
                        <span className="font-mono text-gray-800 dark:text-white/90">{allocationLabel(allocation)}</span>
                        <CopyButton text={allocationLabel(allocation)} />
                        {allocation.isPrimary && <Badge size="sm" color="primary">{t("Servers.Primary")}</Badge>}
                      </span>
                    </TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      <NotesCell allocation={allocation} editable={canUpdate} onSave={(notes) => saveNotes(allocation, notes)} />
                    </TableCell>
                    <TableCell className={cn(tableStyles.bodyCell, "text-end")}>
                      {!allocation.isPrimary && (
                        <span className="inline-flex items-center gap-1">
                          {canUpdate && (
                            <Button variant="outline" size="xs" disabled={busy !== null} onClick={() => makePrimary(allocation)}>
                              <StarIcon className="size-3.5" />
                              {t("ServerNetwork.MakePrimary")}
                            </Button>
                          )}
                          {canDelete && (
                            <Button
                              variant="ghost"
                              size="icon-sm"
                              className="text-red-500 dark:text-red-400"
                              disabled={busy !== null}
                              onClick={() => remove(allocation)}
                              aria-label={t("Common.Remove")}
                              title={t("Common.Remove")}
                            >
                              {busy === allocation.id ? <Spinner className="size-4" /> : <TrashBinIcon className="size-4" />}
                            </Button>
                          )}
                        </span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        )}
      </FeatureCard>

      <p className="flex items-center gap-2 px-1 text-xs text-gray-500 dark:text-gray-400">
        <InfoCircleIcon className="size-4 shrink-0" />
        {t("ServerNetwork.RestartHint")}
      </p>
    </div>
  );
}

export default function ServerNetworkPage() {
  return (
    <ServerPermissionGate permission={ServerPermissions.AllocationRead}>
      <AllocationList />
    </ServerPermissionGate>
  );
}
