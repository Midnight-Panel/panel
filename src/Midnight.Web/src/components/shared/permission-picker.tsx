import { Checkbox } from "@/components/ui/checkbox";
import { useT } from "@/i18n";
import { matchesPermission, Permissions, type PermissionGroup } from "@/lib/permissions";
import { cn } from "@/utils";

/** Grouped checkboxes for role permissions and api key scopes */
export function PermissionPicker({ value, onChange, groups, allowEverything = false }: {
  value: string[];
  onChange: (value: string[]) => void;
  groups: PermissionGroup[];
  allowEverything?: boolean;
}) {
  const { t } = useT();

  const toggle = (permission: string, checked: boolean) =>
    onChange(checked ? [...value.filter((x) => x !== permission), permission] : value.filter((x) => x !== permission));

  const covered = (permission: string) => value.some((x) => x !== permission && x.endsWith("*") && matchesPermission(x, permission));

  const row = (permission: string, label: string, options: { bold?: boolean; danger?: boolean } = {}) => {
    const isCovered = covered(permission);

    return (
      <label className={cn("flex cursor-pointer items-center gap-3 py-1", isCovered && "cursor-default opacity-60")}>
        <Checkbox
          checked={isCovered || value.includes(permission)}
          disabled={isCovered}
          onCheckedChange={(checked) => toggle(permission, Boolean(checked))}
        />
        <span
          className={cn(
            "font-mono",
            options.bold ? "text-sm font-semibold text-gray-800 dark:text-white/90" : "text-xs text-gray-500 dark:text-gray-400",
            options.danger && "text-red-500 dark:text-red-400",
          )}
        >
          {label}
        </span>
      </label>
    );
  };

  return (
    <div className="space-y-3">
      <div className="grid gap-3 sm:grid-cols-2">
        {groups.map((group) => (
          <div key={group.name} className="rounded-xl border border-gray-200 p-4 dark:border-gray-800">
            {row(`${group.name}.*`, `${group.name}.*`, { bold: true })}
            <div className="mt-1 ps-7">{group.permissions.map((permission) => <div key={permission}>{row(permission, permission)}</div>)}</div>
          </div>
        ))}
      </div>
      {allowEverything && row(Permissions.Everything, t("Permissions.Everything"), { bold: true, danger: true })}
    </div>
  );
}
