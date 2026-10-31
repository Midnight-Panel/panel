import { api } from "@/api/client";
import type { DaemonLogs } from "@/api/types";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { OctagonAlertIcon } from "@/icons";
import { cn } from "@/utils";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { useNode } from "./layout";

function lineColor(line: string) {
  if (line.includes(" ERR]") || line.includes(" FTL]")) return "text-red-500 dark:text-red-400";
  if (line.includes(" WRN]")) return "text-amber-600 dark:text-orange-400";
  return "";
}

export default function NodeLogsPage() {
  const node = useNode();
  const { t } = useT();
  const [live, setLive] = useState(true);
  const container = useRef<HTMLPreElement>(null);

  const logs = useQuery({
    queryKey: ["admin", "nodes", node.id, "logs"],
    queryFn: () => api.get<DaemonLogs>(`/api/admin/nodes/${node.id}/logs?lines=500`),
    refetchInterval: live ? 3_000 : false,
    retry: false,
  });

  // Follow the end while live
  useEffect(() => {
    if (live && container.current) container.current.scrollTop = container.current.scrollHeight;
  }, [logs.data, live]);

  return (
    <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/3">
      <div className="flex items-center justify-between border-b border-gray-100 px-5 py-4 dark:border-gray-800">
        <h3 className="text-lg font-semibold text-gray-800 dark:text-white/90">{t("Nodes.Logs")}</h3>
        <label className="flex cursor-pointer items-center gap-3 text-sm text-gray-600 dark:text-gray-400">
          <Switch checked={live} onCheckedChange={(checked) => setLive(Boolean(checked))} />
          {t("Nodes.LiveLogs")}
        </label>
      </div>

      {logs.error ? (
        <div className="p-5">
          <Alert variant="destructive" className="px-4 py-3">
            <OctagonAlertIcon />
            <AlertDescription>{logs.error.message}</AlertDescription>
          </Alert>
        </div>
      ) : !logs.data ? (
        <div className="flex justify-center py-16 text-brand-500"><Spinner className="size-6" /></div>
      ) : (
        <pre ref={container} className="h-[60vh] overflow-auto bg-gray-50 p-5 font-mono text-xs leading-relaxed text-gray-700 dark:bg-black/20 dark:text-gray-300">
          {logs.data.lines.length === 0
            ? t("Table.Empty")
            : logs.data.lines.map((line, index) => <div key={index} className={cn("whitespace-pre-wrap", lineColor(line))}>{line}</div>)}
        </pre>
      )}
    </div>
  );
}
