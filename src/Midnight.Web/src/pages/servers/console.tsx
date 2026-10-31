import { EmptyState } from "@/components/shared/empty-state";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";
import { useT } from "@/i18n";
import { ChevronRightIcon, EyeIcon, LockIcon, RegenerateIcon, SendIcon } from "@/icons";
import { ServerPermissions } from "@/lib/server-permissions";
import { cn } from "@/utils";
import { useState, type KeyboardEvent } from "react";
import { ConsoleTerminal } from "./components/console-terminal";
import { StatCards } from "./components/stat-cards";
import type { ConnectionStatus } from "./console-connection";
import { useServerContext } from "./hooks";

const MAX_HISTORY = 50;

function ConnectionIndicator({ status, onRetry }: { status: ConnectionStatus; onRetry: () => void }) {
  const { t } = useT();

  const styles: Record<ConnectionStatus, { dot: string; label: string }> = {
    connected: { dot: "bg-green-500", label: t("Servers.Connection.Connected") },
    connecting: { dot: "bg-amber-400 animate-pulse", label: t("Servers.Connection.Connecting") },
    reconnecting: { dot: "bg-amber-400 animate-pulse", label: t("Servers.Connection.Reconnecting") },
    closed: { dot: "bg-red-500", label: t("Servers.Connection.Closed") },
  };

  return (
    <span className="flex items-center gap-2 text-xs text-gray-400">
      <span className={cn("size-2 rounded-full", styles[status].dot)} />
      {styles[status].label}
      {status !== "connected" && status !== "connecting" && (
        <button type="button" onClick={onRetry} className="inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-gray-300 hover:bg-white/10 hover:text-white">
          <RegenerateIcon className="size-3" />
          {t("Servers.Connection.Retry")}
        </button>
      )}
    </span>
  );
}

export default function ServerConsolePage() {
  const { server, live, connection, can, sendCommand } = useServerContext();
  const { t } = useT();
  const [command, setCommand] = useState("");
  const [history, setHistory] = useState<string[]>([]);
  const [position, setPosition] = useState<number | null>(null);
  const [draft, setDraft] = useState("");
  const [sending, setSending] = useState(false);

  const installing = live.installing || server.status === "Installing" || live.state === "Installing";
  const running = live.state === "Online" || live.state === "Starting";
  const canConsole = can(ServerPermissions.ControlConsole);
  const canSend = canConsole && running && !server.suspended && !sending;

  const submit = async () => {
    const value = command.trim();
    if (!value || !canSend) return;

    setSending(true);
    const sent = await sendCommand(value);
    setSending(false);

    if (!sent) return;

    setHistory((current) => [value, ...current.filter((entry) => entry !== value)].slice(0, MAX_HISTORY));
    setPosition(null);
    setDraft("");
    setCommand("");
  };

  const onKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === "Enter") {
      event.preventDefault();
      void submit();
      return;
    }

    if (event.key === "ArrowUp" && history.length > 0) {
      event.preventDefault();
      const next = position === null ? 0 : Math.min(history.length - 1, position + 1);
      if (position === null) setDraft(command);
      setPosition(next);
      setCommand(history[next] ?? "");
    }

    if (event.key === "ArrowDown" && position !== null) {
      event.preventDefault();
      const next = position - 1;
      setPosition(next < 0 ? null : next);
      setCommand(next < 0 ? draft : (history[next] ?? ""));
    }
  };

  return (
    <div className="space-y-6">
      {installing && (
        <Alert variant="info" className="px-4 py-3">
          <Spinner className="size-4 text-sky-500" />
          <AlertTitle>{t("Servers.Installing")}</AlertTitle>
          <AlertDescription>{t("Servers.InstallingHint")}</AlertDescription>
        </Alert>
      )}

      {!can(ServerPermissions.WebsocketConnect) ? (
        <EmptyState icon={<LockIcon />} title={t("Servers.ConsoleNoAccess")} description={t("Servers.ConsoleNoAccessHint")} />
      ) : (
      <div className="overflow-hidden rounded-2xl border border-gray-200 bg-[#0e0e10] shadow-theme-xs dark:border-gray-800">
        <div className="flex items-center justify-between gap-3 border-b border-white/[0.06] px-4 py-2.5">
          <span className="flex items-center gap-2 text-xs font-medium text-gray-300">
            <span className="flex gap-1.5">
              <span className="size-2.5 rounded-full bg-white/10" />
              <span className="size-2.5 rounded-full bg-white/10" />
              <span className="size-2.5 rounded-full bg-white/10" />
            </span>
            <span className="ms-2 font-mono">{server.name}</span>
          </span>
          <ConnectionIndicator status={live.connection} onRetry={() => connection.retry()} />
        </div>

        <div className="h-[420px] px-3 pt-2 sm:h-[480px]">
          <ConsoleTerminal connection={connection} />
        </div>

        {canConsole ? (
          <form
            className="flex items-center gap-2 border-t border-white/[0.06] px-4 py-2.5"
            onSubmit={(event) => {
              event.preventDefault();
              void submit();
            }}
          >
            <ChevronRightIcon className="size-4 shrink-0 text-gray-500" />
            <input
              value={command}
              onChange={(event) => {
                setCommand(event.target.value);
                setPosition(null);
              }}
              onKeyDown={onKeyDown}
              disabled={!running || server.suspended}
              maxLength={2048}
              spellCheck={false}
              autoComplete="off"
              aria-label={t("Servers.CommandPlaceholder")}
              placeholder={running ? t("Servers.CommandPlaceholder") : t("Servers.CommandOffline")}
              className="h-8 min-w-0 flex-1 border-0 bg-transparent p-0 font-mono text-[13px] text-gray-100 outline-none placeholder:text-gray-500 focus:ring-0 disabled:cursor-not-allowed"
            />
            <Button
              type="submit"
              size="icon-xs"
              variant="ghost"
              disabled={!canSend || !command.trim()}
              className="text-gray-400 hover:bg-white/10 hover:text-white disabled:bg-transparent disabled:text-gray-600 dark:disabled:bg-transparent"
              aria-label={t("Servers.SendCommand")}
            >
              <SendIcon className="size-4" />
            </Button>
          </form>
        ) : (
          <p className="flex items-center gap-2 border-t border-white/[0.06] px-4 py-3 text-xs text-gray-500">
            <EyeIcon className="size-4 shrink-0" />
            {t("Servers.CommandReadOnly")}
          </p>
        )}
      </div>
      )}

      <StatCards live={live} limits={server.limits} />
    </div>
  );
}
