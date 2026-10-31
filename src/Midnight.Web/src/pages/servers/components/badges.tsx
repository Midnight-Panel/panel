import type { ServerState, ServerStatus } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { CheckIcon, CopyIcon } from "@/icons";
import { copyText, stateTone, statusTone, type StateTone } from "@/lib/servers";
import { cn } from "@/utils";
import { useState } from "react";

const dotColors: Record<StateTone, string> = {
  success: "bg-green-500",
  warning: "bg-amber-500",
  error: "bg-red-500",
  info: "bg-sky-500",
  gray: "bg-gray-400 dark:bg-gray-600",
};

/** Small colored dot of the live state, pulsing while it changes */
export function StateDot({ state, className }: { state: ServerState | undefined; className?: string }) {
  const tone = stateTone(state);
  const pulsing = state === "Starting" || state === "Stopping" || state === "Installing";

  return (
    <span className={cn("relative inline-flex size-2.5 shrink-0", className)}>
      {pulsing && <span className={cn("absolute inline-flex size-full animate-ping rounded-full opacity-60", dotColors[tone])} />}
      <span className={cn("relative inline-flex size-2.5 rounded-full", dotColors[tone])} />
    </span>
  );
}

/** Live container state (Online, Offline, ...); "unknown" when the node is unreachable */
export function ServerStateBadge({ state, nodeOnline = true, size = "sm" }: {
  state: ServerState | undefined;
  nodeOnline?: boolean;
  size?: "sm" | "default";
}) {
  const { t } = useT();

  if (!nodeOnline) return <Badge size={size} color="gray">{t("Servers.NodeUnreachable")}</Badge>;
  if (!state) return <Badge size={size} color="gray">{t("Nodes.Checking")}</Badge>;

  return (
    <Badge size={size} color={stateTone(state)}>
      <StateDot state={state} className="size-2 [&>span]:size-2" />
      {t(`ServerState.${state}`)}
    </Badge>
  );
}

/** Panel lifecycle: Suspended wins over Installing / InstallFailed / Ready */
export function ServerStatusBadge({ status, suspended, size = "sm" }: { status: ServerStatus; suspended: boolean; size?: "sm" | "default" }) {
  const { t } = useT();

  if (suspended) return <Badge size={size} color="warning">{t("Servers.Status.Suspended")}</Badge>;

  return <Badge size={size} color={statusTone(status)}>{t(`Servers.Status.${status}`)}</Badge>;
}

/** Icon button that copies a text and confirms with a check mark */
export function CopyButton({ text, className }: { text: string; className?: string }) {
  const { t } = useT();
  const [copied, setCopied] = useState(false);

  const copy = async () => {
    if (!(await copyText(text))) return;
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };

  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-xs"
      className={cn("size-7 text-gray-500 dark:text-gray-400 [&_svg]:size-3.5", className)}
      onClick={(event) => {
        event.preventDefault();
        event.stopPropagation();
        void copy();
      }}
      aria-label={copied ? t("Common.Copied") : t("Common.Copy")}
      title={copied ? t("Common.Copied") : t("Common.Copy")}
    >
      {copied ? <CheckIcon className="text-green-500" /> : <CopyIcon />}
    </Button>
  );
}
