import { schedulesApi, serverFeatureKeys, type Schedule } from "@/api/server-features";
import { EmptyState } from "@/components/shared/empty-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { CalendarIcon, ChevronRightIcon, PlayIcon, PlusIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Link, useNavigate } from "react-router";
import { CardLoader, FeatureCard, Meta, RelativeTime, ServerPermissionGate } from "../components/feature-ui";
import { useServerContext } from "../hooks";
import { cronExpression, describeCron, saveActive } from "./cron";
import { ScheduleDialog } from "./schedule-dialog";

function ScheduleList() {
  const { server, can } = useServerContext();
  const { t } = useT();
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState<number | null>(null);
  const queryKey = serverFeatureKeys.schedules(server.uuid);

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () => schedulesApi.list(server.uuid),
    refetchInterval: (query) => (query.state.data?.some((schedule) => schedule.isProcessing) ? 5_000 : 60_000),
  });
  const schedules = data ?? [];
  const canUpdate = can(ServerPermissions.ScheduleUpdate) && !server.suspended;

  const toggle = async (schedule: Schedule, isActive: boolean) => {
    setBusy(schedule.id);

    try {
      await saveActive(server.uuid, schedule, isActive);
      await queryClient.invalidateQueries({ queryKey });
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const execute = async (schedule: Schedule) => {
    setBusy(schedule.id);

    try {
      await schedulesApi.execute(server.uuid, schedule.id);
      notifySuccess(t("ServerSchedules.Triggered", schedule.name));
      await queryClient.invalidateQueries({ queryKey });
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(null);
    }
  };

  const createButton = can(ServerPermissions.ScheduleCreate) && (
    <Button size="sm" disabled={server.suspended} onClick={() => setCreating(true)}>
      <PlusIcon className="size-4" />
      {t("ServerSchedules.Create")}
    </Button>
  );

  return (
    <>
      {isLoading ? (
        <FeatureCard title={t("ServerSchedules.Title")}><CardLoader /></FeatureCard>
      ) : schedules.length === 0 ? (
        <EmptyState icon={<CalendarIcon />} title={t("ServerSchedules.Empty")} description={t("ServerSchedules.EmptyHint")}>
          {createButton && <div className="mt-6">{createButton}</div>}
        </EmptyState>
      ) : (
        <FeatureCard title={t("ServerSchedules.Title")} description={t("ServerSchedules.Description")} actions={createButton}>
          <ul className="divide-y divide-gray-100 dark:divide-white/5">
            {schedules.map((schedule) => (
              <li key={schedule.id} className="flex flex-col gap-4 px-5 py-4 lg:flex-row lg:items-center sm:px-6">
                <Link to={String(schedule.id)} className="group flex min-w-0 flex-1 items-center gap-3">
                  <span className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-brand-500/10 text-brand-500 dark:text-brand-400 [&>svg]:size-5">
                    <CalendarIcon />
                  </span>
                  <span className="min-w-0">
                    <span className="flex items-center gap-2">
                      <span className="truncate text-sm font-medium text-gray-800 group-hover:text-brand-500 dark:text-white/90 dark:group-hover:text-brand-400">{schedule.name}</span>
                      {schedule.isProcessing && (
                        <Badge size="sm" color="info">
                          <Spinner className="size-3" />
                          {t("ServerSchedules.Processing")}
                        </Badge>
                      )}
                      {!schedule.isActive && <Badge size="sm" color="gray">{t("ServerSchedules.Inactive")}</Badge>}
                    </span>
                    <span className="mt-0.5 block truncate text-xs text-gray-500 dark:text-gray-400" title={cronExpression(schedule)}>
                      {describeCron(schedule, t)} · {t("ServerSchedules.TaskCount", schedule.tasks.length)}
                    </span>
                  </span>
                </Link>

                <dl className="grid grid-cols-2 gap-4 lg:w-72">
                  <Meta label={t("ServerSchedules.NextRun")}>
                    {schedule.isActive ? <RelativeTime value={schedule.nextRunAt} /> : "-"}
                  </Meta>
                  <Meta label={t("ServerSchedules.LastRun")}>
                    <RelativeTime value={schedule.lastRunAt} fallback={t("ServerSchedules.Never")} />
                  </Meta>
                </dl>

                <div className="flex items-center gap-3">
                  {canUpdate && (
                    <>
                      <Switch
                        size="sm"
                        checked={schedule.isActive}
                        disabled={busy === schedule.id}
                        onCheckedChange={(checked) => toggle(schedule, checked)}
                        aria-label={t("ServerSchedules.Active")}
                      />
                      <Button
                        variant="outline"
                        size="xs"
                        disabled={busy === schedule.id || schedule.isProcessing || schedule.tasks.length === 0}
                        onClick={() => execute(schedule)}
                      >
                        <PlayIcon className="size-3.5" />
                        {t("ServerSchedules.RunNow")}
                      </Button>
                    </>
                  )}
                  <Link
                    to={String(schedule.id)}
                    className="ms-auto inline-flex size-8 items-center justify-center rounded-lg text-gray-500 hover:bg-gray-100 hover:text-gray-800 dark:text-gray-400 dark:hover:bg-white/5 dark:hover:text-white"
                    aria-label={t("ServerSchedules.Open")}
                  >
                    <ChevronRightIcon className="size-4" />
                  </Link>
                </div>
              </li>
            ))}
          </ul>
        </FeatureCard>
      )}

      {creating && <ScheduleDialog open schedule={null} onClose={() => setCreating(false)} onSaved={(created) => navigate(String(created.id))} />}
    </>
  );
}

export default function ServerSchedulesPage() {
  return (
    <ServerPermissionGate permission={ServerPermissions.ScheduleRead}>
      <ScheduleList />
    </ServerPermissionGate>
  );
}
