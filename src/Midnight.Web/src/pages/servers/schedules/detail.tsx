import { ApiError } from "@/api/client";
import { schedulesApi, serverFeatureKeys, type ScheduleTask } from "@/api/server-features";
import { EmptyState } from "@/components/shared/empty-state";
import { SectionCard } from "@/components/shared/section-card";
import { Badge } from "@/components/ui/badge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useT } from "@/i18n";
import { ArchiveIcon, ArrowLeftIcon, BoltIcon, CalendarIcon, ChevronRightIcon, CodeIcon, PencilIcon, PlayIcon, PlusIcon, TaskIcon, TimeIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { ServerPermissions } from "@/lib/server-permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { CardLoader, Meta, RelativeTime, ServerPermissionGate } from "../components/feature-ui";
import { useServerContext } from "../hooks";
import { cronExpression, describeCron, saveActive } from "./cron";
import { ScheduleDialog } from "./schedule-dialog";
import { TaskDialog } from "./task-dialog";

const MAX_TASKS = 10;

const actionIcons: Record<ScheduleTask["action"], ReactNode> = {
  Command: <CodeIcon />,
  Power: <BoltIcon />,
  Backup: <ArchiveIcon />,
};

function TaskRow({ task, index, last, canUpdate, onEdit, onDelete }: {
  task: ScheduleTask;
  index: number;
  last: boolean;
  canUpdate: boolean;
  onEdit: () => void;
  onDelete: () => void;
}) {
  const { t } = useT();

  const payload =
    task.action === "Command" ? <span className="font-mono">{task.payload}</span>
    : task.action === "Power" ? t(`Servers.Power.${task.payload}`)
    : task.payload.trim() ? t("ServerSchedules.BackupWithIgnored", task.payload.trim().split("\n").length)
    : t("ServerSchedules.BackupAll");

  return (
    <li className="relative flex gap-4 pb-6 last:pb-0">
      {!last && <span className="absolute start-[17px] top-10 bottom-0 w-px bg-gray-200 dark:bg-gray-800" />}
      <span className="relative flex size-9 shrink-0 items-center justify-center rounded-xl bg-brand-500/10 text-brand-500 dark:text-brand-400 [&>svg]:size-4">
        {actionIcons[task.action]}
      </span>

      <div className="min-w-0 flex-1 rounded-xl border border-gray-200 p-4 dark:border-gray-800">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div className="min-w-0">
            <p className="flex flex-wrap items-center gap-2 text-sm font-medium text-gray-800 dark:text-white/90">
              <span className="text-xs font-normal text-gray-400 dark:text-gray-500">#{index + 1}</span>
              {t(`ServerSchedules.Actions.${task.action}`)}
              {task.continueOnFailure && <Badge size="sm" color="gray">{t("ServerSchedules.ContinuesOnFailure")}</Badge>}
            </p>
            <p className="mt-1 truncate text-sm text-gray-600 dark:text-gray-300">{payload}</p>
            <p className="mt-2 inline-flex items-center gap-1.5 text-xs text-gray-500 dark:text-gray-400">
              <TimeIcon className="size-3.5" />
              {task.timeOffset > 0
                ? t(index === 0 ? "ServerSchedules.OffsetFirst" : "ServerSchedules.Offset", task.timeOffset)
                : t(index === 0 ? "ServerSchedules.Immediately" : "ServerSchedules.RightAfter")}
            </p>
          </div>
          {canUpdate && (
            <div className="flex shrink-0 items-center gap-1">
              <Button variant="ghost" size="icon-sm" onClick={onEdit} aria-label={t("Common.Edit")} title={t("Common.Edit")}>
                <PencilIcon className="size-4" />
              </Button>
              <Button variant="ghost" size="icon-sm" className="text-red-500 dark:text-red-400" onClick={onDelete} aria-label={t("Common.Delete")} title={t("Common.Delete")}>
                <TrashBinIcon className="size-4" />
              </Button>
            </div>
          )}
        </div>
      </div>
    </li>
  );
}

function ScheduleDetail({ id }: { id: number }) {
  const { server, can } = useServerContext();
  const { t } = useT();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [editing, setEditing] = useState(false);
  const [taskDialog, setTaskDialog] = useState<{ task: ScheduleTask | null } | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: schedule, isLoading, error } = useQuery({
    queryKey: serverFeatureKeys.schedule(server.uuid, id),
    queryFn: () => schedulesApi.get(server.uuid, id),
    refetchInterval: (query) => (query.state.data?.isProcessing ? 3_000 : 60_000),
  });

  const canUpdate = can(ServerPermissions.ScheduleUpdate) && !server.suspended;
  const refresh = () => queryClient.invalidateQueries({ queryKey: serverFeatureKeys.schedules(server.uuid) });

  if (isLoading) return <CardLoader />;

  if (!schedule)
    return (
      <EmptyState
        icon={<CalendarIcon />}
        title={t("ServerSchedules.NotFound")}
        description={error instanceof ApiError ? error.message : undefined}
      >
        <Link to=".." relative="path" className={buttonVariants({ variant: "outline", className: "mt-6" })}>
          <ArrowLeftIcon className="size-4" />
          {t("ServerSchedules.Back")}
        </Link>
      </EmptyState>
    );

  const tasks = [...schedule.tasks].sort((a, b) => a.sequence - b.sequence);

  const run = async (action: () => Promise<unknown>, success?: string) => {
    setBusy(true);

    try {
      await action();
      if (success) notifySuccess(success);
      await refresh();
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  const remove = async () => {
    if (!(await confirm({
      title: t("ServerSchedules.Delete"),
      message: t("ServerSchedules.DeleteConfirm", schedule.name),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setBusy(true);

    try {
      await schedulesApi.delete(server.uuid, schedule.id);
      queryClient.removeQueries({ queryKey: serverFeatureKeys.schedule(server.uuid, id) });
      await queryClient.invalidateQueries({ queryKey: serverFeatureKeys.schedules(server.uuid) });
      notifySuccess(t("ServerSchedules.Deleted", schedule.name));
      navigate("..", { relative: "path" });
    } catch (err) {
      notifyError(err);
      setBusy(false);
    }
  };

  const removeTask = async (task: ScheduleTask) => {
    if (!(await confirm({
      title: t("ServerSchedules.DeleteTask"),
      message: t("ServerSchedules.DeleteTaskConfirm"),
      confirmLabel: t("Common.Delete"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    await run(() => schedulesApi.deleteTask(server.uuid, schedule.id, task.id), t("ServerSchedules.TaskDeleted"));
  };

  return (
    <div className="space-y-6">
      <nav className="flex items-center gap-1.5 text-sm text-gray-500 dark:text-gray-400">
        <Link to=".." relative="path" className="hover:text-gray-800 dark:hover:text-white">{t("Servers.Tabs.Schedules")}</Link>
        <ChevronRightIcon className="size-3.5" />
        <span className="truncate font-medium text-gray-800 dark:text-white/90">{schedule.name}</span>
      </nav>

      <SectionCard
        icon={<CalendarIcon />}
        title={schedule.name}
        description={<span title={cronExpression(schedule)}>{describeCron(schedule, t)} · <span className="font-mono">{cronExpression(schedule)}</span></span>}
        actions={
          <>
            {canUpdate && (
              <>
                <Button size="sm" variant="outline" disabled={busy} onClick={() => setEditing(true)}>
                  <PencilIcon className="size-4" />
                  {t("Common.Edit")}
                </Button>
                <Button
                  size="sm"
                  disabled={busy || schedule.isProcessing || tasks.length === 0}
                  onClick={() => run(() => schedulesApi.execute(server.uuid, schedule.id), t("ServerSchedules.Triggered", schedule.name))}
                >
                  <PlayIcon className="size-4" />
                  {t("ServerSchedules.RunNow")}
                </Button>
              </>
            )}
            {can(ServerPermissions.ScheduleDelete) && !server.suspended && (
              <Button size="icon-sm" variant="outline" className="text-red-500 dark:text-red-400" disabled={busy} onClick={remove} aria-label={t("Common.Delete")} title={t("Common.Delete")}>
                <TrashBinIcon className="size-4" />
              </Button>
            )}
          </>
        }
      >
        <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
          <Meta label={t("Common.Status")}>
            {schedule.isProcessing ? (
              <Badge size="sm" color="info">
                <Spinner className="size-3" />
                {t("ServerSchedules.Processing")}
              </Badge>
            ) : schedule.isActive ? (
              <Badge size="sm" color="success">{t("ServerSchedules.ActiveBadge")}</Badge>
            ) : (
              <Badge size="sm" color="gray">{t("ServerSchedules.Inactive")}</Badge>
            )}
          </Meta>
          <Meta label={t("ServerSchedules.NextRun")}>{schedule.isActive ? <RelativeTime value={schedule.nextRunAt} /> : "-"}</Meta>
          <Meta label={t("ServerSchedules.LastRun")}><RelativeTime value={schedule.lastRunAt} fallback={t("ServerSchedules.Never")} /></Meta>
          <Meta label={t("ServerSchedules.OnlyWhenOnline")}>{schedule.onlyWhenOnline ? t("ServerSchedules.Yes") : t("ServerSchedules.No")}</Meta>
        </dl>
        {canUpdate && (
          <div className="flex justify-end">
            <Button size="xs" variant="ghost" disabled={busy} onClick={() => run(() => saveActive(server.uuid, schedule, !schedule.isActive))}>
              {schedule.isActive ? t("ServerSchedules.Deactivate") : t("ServerSchedules.Activate")}
            </Button>
          </div>
        )}
      </SectionCard>

      <SectionCard
        icon={<TaskIcon />}
        title={t("ServerSchedules.Tasks")}
        description={t("ServerSchedules.TasksHint", tasks.length, MAX_TASKS)}
        actions={canUpdate && (
          <Button size="sm" disabled={busy || tasks.length >= MAX_TASKS} onClick={() => setTaskDialog({ task: null })}>
            <PlusIcon className="size-4" />
            {t("ServerSchedules.AddTask")}
          </Button>
        )}
      >
        {tasks.length === 0 ? (
          <p className="py-6 text-center text-sm text-gray-500 dark:text-gray-400">{t("ServerSchedules.NoTasks")}</p>
        ) : (
          <ol>
            {tasks.map((task, index) => (
              <TaskRow
                key={task.id}
                task={task}
                index={index}
                last={index === tasks.length - 1}
                canUpdate={canUpdate}
                onEdit={() => setTaskDialog({ task })}
                onDelete={() => removeTask(task)}
              />
            ))}
          </ol>
        )}
      </SectionCard>

      {editing && <ScheduleDialog open schedule={schedule} onClose={() => setEditing(false)} />}
      {taskDialog && (
        <TaskDialog
          scheduleId={schedule.id}
          task={taskDialog.task}
          isFirst={taskDialog.task ? tasks[0]?.id === taskDialog.task.id : tasks.length === 0}
          onClose={() => setTaskDialog(null)}
        />
      )}
    </div>
  );
}

export default function ServerScheduleDetailPage() {
  const { scheduleId } = useParams();
  const id = Number(scheduleId);

  return (
    <ServerPermissionGate permission={ServerPermissions.ScheduleRead}>
      {Number.isInteger(id) ? <ScheduleDetail key={id} id={id} /> : null}
    </ServerPermissionGate>
  );
}
