import { adminServersApi } from "@/api/servers";
import {
  Combobox,
  ComboboxChip,
  ComboboxChips,
  ComboboxChipsInput,
  ComboboxContent,
  ComboboxEmpty,
  ComboboxInput,
  ComboboxItem,
  ComboboxList,
  ComboboxValue,
  useComboboxAnchor,
} from "@/components/ui/combobox";
import { useT } from "@/i18n";
import { allocationLabel, type AllocationOption } from "@/lib/servers";
import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";

/** Free allocations of the node (server side search), merged with allocations the server already owns */
function useAllocationOptions(nodeId: number | null, search: string, extra: AllocationOption[], exclude: number[]) {
  const [debounced, setDebounced] = useState("");

  useEffect(() => {
    const timeout = setTimeout(() => setDebounced(search), 250);
    return () => clearTimeout(timeout);
  }, [search]);

  const { data, isFetching } = useQuery({
    queryKey: ["admin", "nodes", nodeId, "allocations", "free", debounced],
    queryFn: () => adminServersApi.freeAllocations(nodeId!, debounced),
    enabled: nodeId !== null,
    placeholderData: keepPreviousData,
  });

  const term = debounced.trim().toLowerCase();
  const own = extra.filter((allocation) => !term || allocationLabel(allocation).includes(term) || allocation.notes?.toLowerCase().includes(term));
  const free = (data?.items ?? []).filter((allocation) => allocation.serverId == null);
  const seen = new Set<number>();

  const items = [...own, ...free].filter((allocation) => {
    if (exclude.includes(allocation.id) || seen.has(allocation.id)) return false;
    seen.add(allocation.id);
    return true;
  });

  items.sort((a, b) => a.ipAddress.localeCompare(b.ipAddress) || a.port - b.port);

  return { items, isFetching };
}

function AllocationItem({ allocation }: { allocation: AllocationOption }) {
  return (
    <ComboboxItem value={allocation}>
      <span className="font-mono">{allocationLabel(allocation)}</span>
      {allocation.notes && <span className="truncate text-xs font-normal text-gray-500 dark:text-gray-400">{allocation.notes}</span>}
    </ComboboxItem>
  );
}

/** Single allocation (the primary ip:port) */
export function AllocationSelect({ nodeId, value, onChange, extra = [], exclude = [], invalid = false }: {
  nodeId: number | null;
  value: AllocationOption | null;
  onChange: (allocation: AllocationOption | null) => void;
  extra?: AllocationOption[];
  exclude?: number[];
  invalid?: boolean;
}) {
  const { t } = useT();
  const [search, setSearch] = useState("");
  const query = value && search === allocationLabel(value) ? "" : search;
  const { items, isFetching } = useAllocationOptions(nodeId, query, extra, exclude);

  return (
    <Combobox<AllocationOption>
      items={items}
      filter={null}
      value={value}
      disabled={nodeId === null}
      onValueChange={(allocation) => onChange(allocation)}
      onInputValueChange={setSearch}
      itemToStringLabel={allocationLabel}
      isItemEqualToValue={(a, b) => a.id === b.id}
    >
      <ComboboxInput
        placeholder={nodeId === null ? t("Servers.SelectNodeFirst") : t("Servers.SearchAllocation")}
        aria-invalid={invalid ? "true" : undefined}
        disabled={nodeId === null}
        className="font-mono"
      />
      <ComboboxContent>
        <ComboboxEmpty>{isFetching ? t("Servers.Searching") : t("Servers.NoFreeAllocations")}</ComboboxEmpty>
        <ComboboxList>{(allocation: AllocationOption) => <AllocationItem key={allocation.id} allocation={allocation} />}</ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}

/** Additional allocations as chips */
export function AllocationMultiSelect({ nodeId, value, onChange, extra = [], exclude = [] }: {
  nodeId: number | null;
  value: AllocationOption[];
  onChange: (allocations: AllocationOption[]) => void;
  extra?: AllocationOption[];
  exclude?: number[];
}) {
  const { t } = useT();
  const anchor = useComboboxAnchor();
  const [search, setSearch] = useState("");
  const { items, isFetching } = useAllocationOptions(nodeId, search, extra, exclude);

  // Selected chips stay resolvable while the search shows other allocations
  const merged = [...value, ...items.filter((item) => !value.some((selected) => selected.id === item.id))];

  return (
    <Combobox<AllocationOption, true>
      multiple
      items={merged}
      filter={null}
      value={value}
      disabled={nodeId === null}
      onValueChange={(allocations) => onChange(allocations)}
      onInputValueChange={setSearch}
      itemToStringLabel={allocationLabel}
      isItemEqualToValue={(a, b) => a.id === b.id}
    >
      <ComboboxChips ref={anchor}>
        <ComboboxValue>
          {(selected: AllocationOption[]) => (
            <>
              {selected.map((allocation) => (
                <ComboboxChip key={allocation.id} className="font-mono">{allocationLabel(allocation)}</ComboboxChip>
              ))}
              <ComboboxChipsInput
                placeholder={nodeId === null ? t("Servers.SelectNodeFirst") : selected.length === 0 ? t("Servers.AddAllocations") : ""}
                disabled={nodeId === null}
              />
            </>
          )}
        </ComboboxValue>
      </ComboboxChips>
      <ComboboxContent anchor={anchor}>
        <ComboboxEmpty>{isFetching ? t("Servers.Searching") : t("Servers.NoFreeAllocations")}</ComboboxEmpty>
        <ComboboxList>{(allocation: AllocationOption) => <AllocationItem key={allocation.id} allocation={allocation} />}</ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}
