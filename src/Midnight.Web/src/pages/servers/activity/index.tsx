import { serverActivityApi, serverFeatureKeys, type ServerActivity } from "@/api/server-features";
import { EmptyState } from "@/components/shared/empty-state";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { ChevronLeftIcon, ChevronRightIcon, GlobeIcon, ListIcon, SystemIcon } from "@/icons";
import { ServerPermissions } from "@/lib/server-permissions";
import { cn } from "@/utils";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { CardLoader, FeatureCard, RelativeTime, ServerPermissionGate } from "../components/feature-ui";
import { useServerContext } from "../hooks";

const PAGE_SIZE = 25;

/** "server:file.write" => "ServerActivity.Events.file_write" */
function eventKey(event: string) {
  return `ServerActivity.Events.${event.replace(/^server:/, "").replace(/[.:-]/g, "_")}`;
}

/** Fallback for unknown events: "server:file.create-folder" => "File create folder" */
function humanize(event: string) {
  const text = event.replace(/^server:/, "").replace(/[._:-]+/g, " ").trim();
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function tone(event: string) {
  const name = event.replace(/^server:/, "");
  if (/delete|kill|remove/.test(name)) return "bg-red-500";
  if (name.startsWith("power")) return "bg-amber-500";
  if (name.startsWith("backup")) return "bg-sky-500";
  return "bg-brand-500";
}

function ActivityRow({ entry, label }: { entry: ServerActivity; label: string }) {
  const { t } = useT();
  const properties = Object.entries(entry.properties ?? {});

  return (
    <li className="relative flex gap-4 px-5 py-4 sm:px-6">
      <div className="relative flex shrink-0 flex-col items-center">
        {entry.userId != null && entry.username ? (
          <UserAvatar userId={entry.userId} hasAvatar={false} name={entry.username} size="sm" />
        ) : (
          <span className="flex size-8 items-center justify-center rounded-full bg-gray-100 text-gray-500 dark:bg-white/5 dark:text-gray-400 [&>svg]:size-4">
            <SystemIcon />
          </span>
        )}
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex flex-col gap-1 sm:flex-row sm:items-start sm:justify-between sm:gap-4">
          <p className="flex min-w-0 items-center gap-2 text-sm text-gray-800 dark:text-white/90">
            <span className={cn("size-1.5 shrink-0 rounded-full", tone(entry.event))} />
            <span className="font-medium">{entry.username ?? t("ServerActivity.System")}</span>
            <span className="truncate text-gray-600 dark:text-gray-300">{label}</span>
          </p>
          <span className="shrink-0 text-xs text-gray-500 dark:text-gray-400">
            <RelativeTime value={entry.createdAt} />
          </span>
        </div>

        <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
          <span className="font-mono text-[11px] text-gray-400 dark:text-gray-500">{entry.event}</span>
          {entry.ipAddress && (
            <span className="inline-flex items-center gap-1 font-mono text-[11px] text-gray-400 dark:text-gray-500">
              <GlobeIcon className="size-3" />
              {entry.ipAddress}
            </span>
          )}
        </div>

        {properties.length > 0 && (
          <div className="mt-2 flex flex-wrap gap-1.5">
            {properties.map(([key, value]) => (
              <span
                key={key}
                className="inline-flex max-w-full items-center gap-1 rounded-md border border-gray-200 bg-gray-50 px-2 py-0.5 text-[11px] dark:border-gray-800 dark:bg-white/[0.03]"
              >
                <span className="text-gray-500 dark:text-gray-400">{key}</span>
                <span className="truncate font-mono text-gray-700 dark:text-gray-200">{value}</span>
              </span>
            ))}
          </div>
        )}
      </div>
    </li>
  );
}

function ActivityList() {
  const { server } = useServerContext();
  const { t } = useT();
  const { i18n } = useTranslation();
  const [page, setPage] = useState(1);

  const { data, isLoading, isFetching } = useQuery({
    queryKey: [...serverFeatureKeys.activity(server.uuid), page],
    queryFn: () => serverActivityApi.list(server.uuid, page, PAGE_SIZE),
    placeholderData: keepPreviousData,
    refetchInterval: page === 1 ? 30_000 : false,
  });

  const label = (event: string) => {
    const key = eventKey(event);
    return i18n.exists(key) ? t(key) : humanize(event);
  };

  if (isLoading) return <FeatureCard title={t("ServerActivity.Title")}><CardLoader /></FeatureCard>;

  if (!data || data.items.length === 0)
    return <EmptyState icon={<ListIcon />} title={t("ServerActivity.Empty")} description={t("ServerActivity.EmptyHint")} />;

  const totalPages = Math.max(1, data.totalPages);

  return (
    <FeatureCard title={t("ServerActivity.Title")} description={t("ServerActivity.Description")}>
      <ul className={cn("divide-y divide-gray-100 dark:divide-white/5", isFetching && "opacity-70")}>
        {data.items.map((entry) => <ActivityRow key={entry.id} entry={entry} label={label(entry.event)} />)}
      </ul>

      {totalPages > 1 && (
        <div className="flex items-center justify-between gap-3 border-t border-gray-100 px-5 py-4 sm:px-6 dark:border-gray-800">
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("ServerActivity.Page", data.page, totalPages)}</p>
          <div className="flex items-center gap-1">
            <Button variant="outline" size="icon-sm" disabled={page <= 1} onClick={() => setPage(page - 1)} aria-label={t("Table.Previous")}>
              <ChevronLeftIcon />
            </Button>
            <Button variant="outline" size="icon-sm" disabled={page >= totalPages} onClick={() => setPage(page + 1)} aria-label={t("Table.Next")}>
              <ChevronRightIcon />
            </Button>
          </div>
        </div>
      )}
    </FeatureCard>
  );
}

export default function ServerActivityPage() {
  return (
    <ServerPermissionGate permission={ServerPermissions.ActivityRead}>
      <ActivityList />
    </ServerPermissionGate>
  );
}
