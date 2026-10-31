import type { ServerPermissionGroup } from "@/api/server-features";
import { Checkbox } from "@/components/ui/checkbox";
import { Spinner } from "@/components/ui/spinner";
import { useT } from "@/i18n";
import { cn } from "@/utils";

/**
 * Grouped checkboxes of the server permissions with descriptions and "select all" per group.
 * Permissions the current user doesn't have can't be granted (disabled, the api enforces the same).
 */
export function PermissionEditor({ value, onChange, grantable, groups }: {
  value: string[];
  onChange: (value: string[]) => void;
  grantable: (permission: string) => boolean;
  groups: ServerPermissionGroup[] | undefined;
}) {
  const { t } = useT();

  if (!groups)
    return (
      <div className="flex justify-center py-8 text-brand-500">
        <Spinner className="size-6" />
      </div>
    );

  const set = (permissions: string[], checked: boolean) =>
    onChange(checked ? [...new Set([...value, ...permissions])] : value.filter((permission) => !permissions.includes(permission)));

  return (
    <div className="grid gap-3 sm:grid-cols-2">
      {groups.map((group) => {
        const allowed = group.permissions.map((permission) => permission.key).filter(grantable);
        const selected = allowed.filter((permission) => value.includes(permission));
        const all = allowed.length > 0 && selected.length === allowed.length;

        return (
          <div key={group.key} className="rounded-xl border border-gray-200 p-4 dark:border-gray-800">
            <label className={cn("flex items-center justify-between gap-3", allowed.length > 0 ? "cursor-pointer" : "cursor-default opacity-60")}>
              <span className="text-sm font-semibold text-gray-800 dark:text-white/90">{group.name}</span>
              <span className="flex items-center gap-2 text-xs text-gray-500 dark:text-gray-400">
                {t("ServerUsers.SelectAll")}
                <Checkbox
                  checked={all}
                  indeterminate={!all && selected.length > 0}
                  disabled={allowed.length === 0}
                  onCheckedChange={(checked) => set(allowed, Boolean(checked))}
                />
              </span>
            </label>

            <div className="mt-3 space-y-2.5 border-t border-gray-100 pt-3 dark:border-gray-800/80">
              {group.permissions.map((permission) => {
                const enabled = grantable(permission.key);

                return (
                  <label
                    key={permission.key}
                    className={cn("flex items-start gap-3", enabled ? "cursor-pointer" : "cursor-not-allowed opacity-50")}
                    title={enabled ? undefined : t("ServerUsers.NotGrantable")}
                  >
                    <Checkbox
                      className="mt-0.5"
                      checked={value.includes(permission.key)}
                      disabled={!enabled}
                      onCheckedChange={(checked) => set([permission.key], Boolean(checked))}
                    />
                    <span className="min-w-0">
                      <span className="block text-sm text-gray-700 dark:text-gray-300">{permission.description}</span>
                      <span className="block font-mono text-[11px] text-gray-400 dark:text-gray-500">{permission.key}</span>
                    </span>
                  </label>
                );
              })}
            </div>
          </div>
        );
      })}
    </div>
  );
}
