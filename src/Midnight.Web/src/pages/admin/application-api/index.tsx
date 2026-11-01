import { ApplicationResources, applicationKeysApi, type ApplicationKey, type CreatedApplicationKey } from "@/api/application-keys";
import { useAuth } from "@/auth/AuthContext";
import { tableStyles } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { PageHeader } from "@/components/shared/page-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { useT } from "@/i18n";
import { CodeIcon, PencilIcon, PlusIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { RelativeTime } from "@/pages/servers/components/feature-ui";
import { cn } from "@/utils";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { KeyDialog } from "./key-dialog";
import { applicationKeysQueryKey, fullPermissions } from "./resources";
import { TokenDialog } from "./token-dialog";

function PermissionSummary({ applicationKey }: { applicationKey: ApplicationKey }) {
  const { t } = useT();
  const permissions = fullPermissions(applicationKey.permissions);
  const granted = ApplicationResources.filter((resource) => permissions[resource] !== "None");

  if (granted.length === 0) return <Badge size="sm" color="gray">{t("ApplicationApi.NoAccess")}</Badge>;

  return (
    <div className="flex max-w-md flex-wrap gap-1">
      {granted.map((resource) => (
        <Badge key={resource} size="sm" color={permissions[resource] === "ReadWrite" ? "primary" : "gray"}>
          {t(permissions[resource] === "ReadWrite" ? "ApplicationApi.Summary.ReadWrite" : "ApplicationApi.Summary.Read", t(`ApplicationApi.Resources.${resource}`))}
        </Badge>
      ))}
    </div>
  );
}

function AllowedIps({ ips }: { ips: string[] }) {
  const { t } = useT();

  if (ips.length === 0) return <span className="text-gray-500 dark:text-gray-400">{t("ApplicationApi.AnyIp")}</span>;

  const shown = ips.slice(0, 2);

  return (
    <div className="flex flex-col gap-0.5 font-mono text-xs" title={ips.join("\n")}>
      {shown.map((ip) => <span key={ip}>{ip}</span>)}
      {ips.length > shown.length && <span className="font-sans text-gray-500 dark:text-gray-400">{t("ApplicationApi.MoreIps", ips.length - shown.length)}</span>}
    </div>
  );
}

/** /admin/api – the route is guarded with admin.api.read in App.tsx */
export default function ApplicationApiPage() {
  const { t, formatDate } = useT();
  const { can } = useAuth();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [dialog, setDialog] = useState<{ applicationKey: ApplicationKey | null } | null>(null);
  const [created, setCreated] = useState<CreatedApplicationKey | null>(null);
  const canWrite = can(Permissions.AdminApiWrite);

  const { data, isLoading } = useQuery({ queryKey: applicationKeysQueryKey, queryFn: applicationKeysApi.list });
  const keys = data ?? [];

  const remove = async (applicationKey: ApplicationKey) => {
    if (!(await confirm({
      title: t("ApplicationApi.Delete"),
      message: t("ApplicationApi.DeleteConfirm", applicationKey.identifier),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    try {
      await applicationKeysApi.delete(applicationKey.id);
      await queryClient.invalidateQueries({ queryKey: applicationKeysQueryKey });
      notifySuccess(t("ApplicationApi.Deleted", applicationKey.identifier));
    } catch (error) {
      notifyError(error);
    }
  };

  const createButton = canWrite && (
    <Button onClick={() => setDialog({ applicationKey: null })}>
      <PlusIcon />
      {t("ApplicationApi.Create")}
    </Button>
  );

  const description = (
    <>
      {t("ApplicationApi.Description")}{" "}
      <a href="/api/reference" target="_blank" rel="noreferrer" className="font-medium text-brand-500 underline-offset-4 hover:underline dark:text-brand-400">
        {t("ApplicationApi.Reference")}
      </a>
    </>
  );

  return (
    <>
      <PageHeader icon={<CodeIcon />} title={t("Nav.Admin.ApplicationApi")} description={description} actions={createButton} />

      {isLoading ? (
        <div className="flex justify-center py-20 text-brand-500"><Spinner className="size-8" /></div>
      ) : keys.length === 0 ? (
        <EmptyState icon={<CodeIcon />} title={t("ApplicationApi.Empty")} description={t("ApplicationApi.EmptyHint")}>
          {createButton && <div className="mt-5">{createButton}</div>}
        </EmptyState>
      ) : (
        <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
          <div className="max-w-full overflow-x-auto">
            <Table>
              <TableHeader className="border-b border-gray-100 dark:border-white/5">
                <TableRow>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApplicationApi.Key")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApplicationApi.Permissions")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApplicationApi.AllowedIps")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApplicationApi.CreatedBy")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApplicationApi.Created")}</TableCell>
                  <TableCell isHeader className={tableStyles.headerCell}>{t("ApplicationApi.LastUsed")}</TableCell>
                  <TableCell isHeader className={cn(tableStyles.headerCell, "text-end")}>{""}</TableCell>
                </TableRow>
              </TableHeader>
              <TableBody className="divide-y divide-gray-100 dark:divide-white/5">
                {keys.map((applicationKey) => (
                  <TableRow key={applicationKey.id}>
                    <TableCell className={tableStyles.bodyCell}>
                      <div className="min-w-0">
                        <p className="font-mono text-xs font-medium text-gray-800 dark:text-white/90">{applicationKey.identifier}</p>
                        <p className="mt-0.5 max-w-xs truncate text-sm text-gray-500 dark:text-gray-400" title={applicationKey.description}>{applicationKey.description}</p>
                      </div>
                    </TableCell>
                    <TableCell className={tableStyles.bodyCell}><PermissionSummary applicationKey={applicationKey} /></TableCell>
                    <TableCell className={tableStyles.bodyCell}><AllowedIps ips={applicationKey.allowedIps} /></TableCell>
                    <TableCell className={tableStyles.bodyCell}>
                      {applicationKey.createdByUsername ?? <span className="text-gray-500 dark:text-gray-400">{t("ApplicationApi.DeletedUser")}</span>}
                    </TableCell>
                    <TableCell className={cn(tableStyles.bodyCell, "whitespace-nowrap")}>{formatDate(applicationKey.createdAt, false)}</TableCell>
                    <TableCell className={tableStyles.bodyCell}><RelativeTime value={applicationKey.lastUsedAt} fallback={t("ApplicationApi.NeverUsed")} /></TableCell>
                    <TableCell className={cn(tableStyles.bodyCell, "text-end")}>
                      {canWrite && (
                        <span className="inline-flex items-center gap-1">
                          <Button variant="ghost" size="icon-sm" onClick={() => setDialog({ applicationKey })} aria-label={t("Common.Edit")} title={t("Common.Edit")}>
                            <PencilIcon className="size-4" />
                          </Button>
                          <Button variant="ghost" size="icon-sm" className="text-red-500 dark:text-red-400" onClick={() => remove(applicationKey)} aria-label={t("Common.Delete")} title={t("Common.Delete")}>
                            <TrashBinIcon className="size-4" />
                          </Button>
                        </span>
                      )}
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </div>
        </div>
      )}

      {dialog && <KeyDialog applicationKey={dialog.applicationKey} onClose={() => setDialog(null)} onCreated={setCreated} />}
      {created && <TokenDialog created={created} onClose={() => setCreated(null)} />}
    </>
  );
}
