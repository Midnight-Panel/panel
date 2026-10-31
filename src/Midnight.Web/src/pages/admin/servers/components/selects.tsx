import { adminServersApi } from "@/api/servers";
import type { ShardSummary } from "@/api/types";
import { Select, SelectContent, SelectGroup, SelectItem, SelectLabel, SelectSeparator, SelectTrigger, SelectValue } from "@/components/ui/select";
import { useT } from "@/i18n";
import { useQuery } from "@tanstack/react-query";
import { Fragment } from "react";
import { useShardList } from "../hooks";

/** Node select (first 100 nodes by name) */
export function NodeSelect({ value, onChange, invalid = false }: { value: number | null; onChange: (nodeId: number) => void; invalid?: boolean }) {
  const { t } = useT();
  const { data } = useQuery({ queryKey: ["admin", "nodes", "list", "select"], queryFn: adminServersApi.listNodes });
  const nodes = data?.items ?? [];

  return (
    <Select<number>
      value={value}
      items={nodes.map((node) => ({ value: node.id, label: node.name }))}
      onValueChange={(nodeId) => nodeId !== null && onChange(nodeId)}
    >
      <SelectTrigger aria-invalid={invalid ? "true" : undefined}>
        <SelectValue placeholder={t("Servers.SelectNode")} />
      </SelectTrigger>
      <SelectContent>
        {nodes.length === 0 && <p className="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">{t("Servers.NoNodes")}</p>}
        {nodes.map((node) => (
          <SelectItem key={node.id} value={node.id}>
            <span>{node.name}</span>
            <span className="font-mono text-xs font-normal text-gray-500 dark:text-gray-400">{node.fqdn}</span>
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

/** Shard select grouped by collection */
export function ShardSelect({ value, onChange, invalid = false }: { value: number | null; onChange: (shardId: number) => void; invalid?: boolean }) {
  const { t } = useT();
  const { data } = useShardList();
  const shards = data?.items ?? [];

  const groups = new Map<string, ShardSummary[]>();
  for (const shard of shards) groups.set(shard.collectionName, [...(groups.get(shard.collectionName) ?? []), shard]);
  const sorted = [...groups.entries()].sort(([a], [b]) => a.localeCompare(b));

  return (
    <Select<number>
      value={value}
      items={shards.map((shard) => ({ value: shard.id, label: shard.name }))}
      onValueChange={(shardId) => shardId !== null && onChange(shardId)}
    >
      <SelectTrigger aria-invalid={invalid ? "true" : undefined}>
        <SelectValue placeholder={t("Servers.SelectShard")} />
      </SelectTrigger>
      <SelectContent>
        {shards.length === 0 && <p className="px-3 py-2 text-sm text-gray-500 dark:text-gray-400">{t("Servers.NoShards")}</p>}
        {sorted.map(([collection, items], index) => (
          <Fragment key={collection}>
            {index > 0 && <SelectSeparator />}
            <SelectGroup>
              <SelectLabel>{collection}</SelectLabel>
              {items.map((shard) => (
                <SelectItem key={shard.id} value={shard.id}>
                  <span>{shard.name}</span>
                  <span className="text-xs font-normal text-gray-500 dark:text-gray-400">v{shard.version}</span>
                </SelectItem>
              ))}
            </SelectGroup>
          </Fragment>
        ))}
      </SelectContent>
    </Select>
  );
}

/** Image select of a shard (label => image) */
export function DockerImageSelect({ images, value, onChange, disabled = false }: {
  images: Record<string, string>;
  value: string;
  onChange: (image: string) => void;
  disabled?: boolean;
}) {
  const { t } = useT();
  const entries = Object.entries(images);
  // Keep a custom image of the server selectable even when the shard doesn't list it anymore
  if (value && !entries.some(([, image]) => image === value)) entries.push([value, value]);

  return (
    <Select<string>
      value={value || null}
      disabled={disabled}
      items={entries.map(([label, image]) => ({ value: image, label }))}
      onValueChange={(image) => image !== null && onChange(image)}
    >
      <SelectTrigger>
        <SelectValue placeholder={t("Servers.SelectImage")} />
      </SelectTrigger>
      <SelectContent>
        {entries.map(([label, image]) => (
          <SelectItem key={image} value={image}>
            <span>{label}</span>
            {label !== image && <span className="font-mono text-xs font-normal text-gray-500 dark:text-gray-400">{image}</span>}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
