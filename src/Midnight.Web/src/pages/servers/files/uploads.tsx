import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { CheckCircleIcon, OctagonAlertIcon, UploadIcon, XIcon } from "@/icons";
import { formatBytes } from "@/lib/format";
import { cn } from "@/utils";
import { useState } from "react";
import type { UploadItem } from "./use-uploads";

/** Floating progress list, bottom right */
export function UploadPanel({ items, onCancel, onCancelAll, onClear }: {
  items: UploadItem[];
  onCancel: (id: number) => void;
  onCancelAll: () => void;
  onClear: () => void;
}) {
  const { t } = useT();
  const [collapsed, setCollapsed] = useState(false);

  if (items.length === 0) return null;

  const running = items.filter((item) => item.status === "queued" || item.status === "uploading");
  const failed = items.filter((item) => item.status === "error").length;
  const total = items.reduce((sum, item) => sum + item.size, 0);
  const loaded = items.reduce((sum, item) => sum + (item.status === "done" ? item.size : item.loaded), 0);
  const overall = total > 0 ? Math.round((loaded / total) * 100) : 100;

  return (
    <div className="fixed inset-e-4 bottom-4 z-50 w-[min(24rem,calc(100vw-2rem))] overflow-hidden rounded-2xl border border-gray-200 bg-white shadow-theme-lg dark:border-gray-800 dark:bg-[#161619]">
      <div className="flex items-center gap-3 border-b border-gray-100 px-4 py-3 dark:border-gray-800">
        <span className="flex size-8 shrink-0 items-center justify-center rounded-lg bg-brand-500/10 text-brand-500 dark:text-brand-400">
          <UploadIcon className="size-4" />
        </span>
        <button type="button" className="min-w-0 flex-1 text-start" onClick={() => setCollapsed((value) => !value)}>
          <p className="truncate text-sm font-semibold text-gray-900 dark:text-white">
            {running.length > 0 ? t("Files.Upload.Uploading", running.length) : t("Files.Upload.Finished")}
          </p>
          <p className="text-xs text-gray-500 dark:text-gray-400">
            {failed > 0 ? t("Files.Upload.Failed", failed) : `${overall}% · ${formatBytes(loaded)} / ${formatBytes(total)}`}
          </p>
        </button>
        {running.length > 0 ? (
          <Button size="xs" variant="ghost" onClick={onCancelAll}>{t("Files.Upload.CancelAll")}</Button>
        ) : (
          <Button size="icon-xs" variant="ghost" onClick={onClear} aria-label={t("Files.Upload.Close")}>
            <XIcon className="size-4" />
          </Button>
        )}
      </div>

      {!collapsed && (
        <ul className="max-h-72 divide-y divide-gray-100 overflow-y-auto dark:divide-white/5">
          {items.map((item) => {
            const percent = item.size > 0 ? Math.min(100, Math.round((item.loaded / item.size) * 100)) : item.status === "done" ? 100 : 0;

            return (
              <li key={item.id} className="px-4 py-2.5">
                <div className="flex items-center gap-2">
                  <span className="min-w-0 flex-1 truncate text-sm text-gray-800 dark:text-white/90" title={`${item.directory}/${item.name}`.replace("//", "/")}>
                    {item.name}
                  </span>
                  {item.status === "done" && <CheckCircleIcon className="size-4 shrink-0 text-green-500" />}
                  {item.status === "error" && <OctagonAlertIcon className="size-4 shrink-0 text-red-500" />}
                  {(item.status === "queued" || item.status === "uploading") && (
                    <button
                      type="button"
                      onClick={() => onCancel(item.id)}
                      aria-label={t("Common.Cancel")}
                      className="rounded-md p-0.5 text-gray-400 hover:bg-gray-100 hover:text-gray-700 dark:hover:bg-white/5 dark:hover:text-gray-200"
                    >
                      <XIcon className="size-3.5" />
                    </button>
                  )}
                </div>
                <div className="mt-1.5 flex items-center gap-2">
                  <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-gray-100 dark:bg-white/5">
                    <div
                      className={cn(
                        "h-full rounded-full transition-[width] duration-200",
                        item.status === "error" ? "bg-red-500" : item.status === "cancelled" ? "bg-gray-400 dark:bg-gray-600" : item.status === "done" ? "bg-green-500" : "bg-brand-500",
                      )}
                      style={{ width: `${item.status === "error" || item.status === "cancelled" ? 100 : percent}%` }}
                    />
                  </div>
                  <span className="w-28 shrink-0 text-end text-[11px] text-gray-500 tabular-nums dark:text-gray-400">
                    {item.status === "queued" && t("Files.Upload.Queued")}
                    {item.status === "uploading" && `${percent}% · ${formatBytes(item.size)}`}
                    {item.status === "done" && formatBytes(item.size)}
                    {item.status === "cancelled" && t("Files.Upload.Cancelled")}
                    {item.status === "error" && t("Files.Upload.Error")}
                  </span>
                </div>
                {item.error && <p className="mt-1 text-xs text-red-500">{item.error}</p>}
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
