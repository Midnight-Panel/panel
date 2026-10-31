import type { PowerAction, ServerState } from "@/api/types";
import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown";
import { useT } from "@/i18n";
import { BoltIcon, ChevronDownIcon, PlayIcon, RegenerateIcon, XIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { powerAvailability } from "@/lib/servers";
import { useState } from "react";

/**
 * Start / Restart / Stop, Kill in a dropdown. Buttons are disabled when the action makes no sense in the state
 * and hidden without the permission (control.start / control.restart / control.stop for stop and kill).
 */
export function PowerButtons({ state, allowed, blocked, onPower }: {
  state: ServerState | undefined;
  /** Actions the user may run */
  allowed: PowerAction[];
  /** Suspended, installing, install failed or the node is unreachable */
  blocked: boolean;
  onPower: (action: PowerAction) => Promise<void>;
}) {
  const { t } = useT();
  const confirm = useConfirm();
  const [pending, setPending] = useState<PowerAction | null>(null);
  const can = powerAvailability(state, blocked || pending !== null);

  const run = async (action: PowerAction) => {
    if (action === "Kill" && !(await confirm({
      title: t("Servers.Power.Kill"),
      message: t("Servers.Power.KillConfirm"),
      confirmLabel: t("Servers.Power.Kill"),
      cancelLabel: t("Common.Cancel"),
    })))
      return;

    setPending(action);

    try {
      await onPower(action);
    } finally {
      setPending(null);
    }
  };

  return (
    <div className="flex items-center gap-2">
      {allowed.includes("Start") && (
        <Button size="sm" disabled={!can.start} onClick={() => run("Start")}>
          <PlayIcon className="size-4" />
          {t("Servers.Power.Start")}
        </Button>
      )}
      {allowed.includes("Restart") && (
        <Button size="sm" variant="outline" disabled={!can.restart} onClick={() => run("Restart")}>
          <RegenerateIcon className="size-4" />
          {t("Servers.Power.Restart")}
        </Button>
      )}
      {allowed.includes("Stop") && <div className="flex items-center">
        <Button
          size="sm"
          variant="outline"
          className="rounded-e-none border-e-0 text-red-600 hover:text-red-700 dark:text-red-400 dark:hover:text-red-300"
          disabled={!can.stop}
          onClick={() => run("Stop")}
        >
          <XIcon className="size-4" />
          {t("Servers.Power.Stop")}
        </Button>
        <DropdownMenu>
          <DropdownMenuTrigger
            render={<Button size="icon-sm" variant="outline" className="rounded-s-none" aria-label={t("Servers.Power.More")} />}
          >
            <ChevronDownIcon className="size-4" />
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end" className="w-56 p-2">
            <DropdownMenuItem variant="destructive" disabled={!can.kill} onClick={() => run("Kill")}>
              <BoltIcon className="size-4" />
              <span>
                <span className="block">{t("Servers.Power.Kill")}</span>
                <span className="block text-xs font-normal text-gray-500 dark:text-gray-400">{t("Servers.Power.KillHint")}</span>
              </span>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>}
    </div>
  );
}
