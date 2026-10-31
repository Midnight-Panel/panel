import { adminServersApi } from "@/api/servers";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Combobox, ComboboxContent, ComboboxEmpty, ComboboxInput, ComboboxItem, ComboboxList } from "@/components/ui/combobox";
import { useT } from "@/i18n";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";

export interface UserOption {
  id: number;
  username: string;
  email: string;
  hasAvatar?: boolean;
}

/** Searchable owner select, searches GET /api/admin/users?search= */
export function UserPicker({ value, onChange, invalid = false, disabled = false }: {
  value: UserOption | null;
  onChange: (user: UserOption | null) => void;
  invalid?: boolean;
  disabled?: boolean;
}) {
  const { t } = useT();
  const [search, setSearch] = useState("");
  const [debounced, setDebounced] = useState("");

  useEffect(() => {
    const timeout = setTimeout(() => setDebounced(search), 250);
    return () => clearTimeout(timeout);
  }, [search]);

  // The input shows the selected username, don't use it as filter
  const query = value && debounced === value.username ? "" : debounced;

  const { data, isFetching } = useQuery({
    queryKey: ["admin", "users", "picker", query],
    queryFn: () => adminServersApi.searchUsers(query),
    placeholderData: keepPreviousData,
  });

  const items: UserOption[] = (data?.items ?? []).map((user) => ({ id: user.id, username: user.username, email: user.email, hasAvatar: user.hasAvatar }));

  return (
    <Combobox<UserOption>
      items={items}
      filter={null}
      value={value}
      disabled={disabled}
      onValueChange={(user) => onChange(user)}
      onInputValueChange={setSearch}
      itemToStringLabel={(user) => user.username}
      isItemEqualToValue={(a, b) => a.id === b.id}
    >
      <ComboboxInput placeholder={t("Servers.SearchUser")} aria-invalid={invalid ? "true" : undefined} disabled={disabled} />
      <ComboboxContent>
        <ComboboxEmpty>{isFetching ? t("Servers.Searching") : t("Servers.NoUsersFound")}</ComboboxEmpty>
        <ComboboxList>
          {(user: UserOption) => (
            <ComboboxItem key={user.id} value={user}>
              <UserAvatar userId={user.id} hasAvatar={user.hasAvatar ?? false} name={user.username} size="xs" />
              <span className="min-w-0">
                <span className="block truncate text-gray-800 dark:text-white/90">{user.username}</span>
                <span className="block truncate text-xs font-normal text-gray-500 dark:text-gray-400">{user.email}</span>
              </span>
            </ComboboxItem>
          )}
        </ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}
