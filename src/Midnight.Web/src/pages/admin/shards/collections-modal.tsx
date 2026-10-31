import { shardsApi } from "@/api/shards";
import type { ShardCollection, ShardCollectionRequest } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import { PencilIcon, PlusIcon, TrashBinIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";

interface CollectionsModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function CollectionsModal({ open, onOpenChange }: CollectionsModalProps) {
  const { t } = useT();
  const queryClient = useQueryClient();
  const [editingId, setEditingId] = useState<number | null>(null);
  const [busy, setBusy] = useState(false);

  const { data: collections = [], isLoading } = useQuery({
    queryKey: ["admin", "shards", "collections", "all"],
    queryFn: () => shardsApi.listAllCollections(),
    enabled: open,
  });

  const form = useForm<ShardCollectionRequest>({
    defaultValues: { name: "", author: "Midnightpanel", description: "" },
  });

  const handleStartCreate = () => {
    setEditingId(0);
    form.reset({ name: "", author: "Midnightpanel", description: "" });
  };

  const handleStartEdit = (col: ShardCollection) => {
    setEditingId(col.id);
    form.reset({ name: col.name, author: col.author, description: col.description || "" });
  };

  const handleCancelForm = () => {
    setEditingId(null);
  };

  const handleSave = async (values: ShardCollectionRequest) => {
    setBusy(true);
    try {
      if (editingId === 0) {
        await shardsApi.createCollection(values);
        notifySuccess(t("Common.Saved"));
      } else if (editingId !== null) {
        await shardsApi.updateCollection(editingId, values);
        notifySuccess(t("Common.Saved"));
      }
      await queryClient.invalidateQueries({ queryKey: ["admin", "shards", "collections"] });
      setEditingId(null);
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (col: ShardCollection) => {
    if (!confirm(`Are you sure you want to delete collection "${col.name}"?`)) return;
    try {
      await shardsApi.deleteCollection(col.id);
      notifySuccess(t("Shards.CollectionDeleted"));
      await queryClient.invalidateQueries({ queryKey: ["admin", "shards", "collections"] });
    } catch (err) {
      notifyError(err);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-2xl bg-[#0F172A] border-white/10 text-white">
        <DialogHeader>
          <DialogTitle className="flex items-center justify-between text-xl font-bold">
            <span>{t("Shards.ManageCollections")}</span>
            {editingId === null && (
              <Button size="sm" onClick={handleStartCreate} className="bg-indigo-600 hover:bg-indigo-500">
                <PlusIcon className="size-4 mr-1.5" />
                {t("Shards.CreateCollection")}
              </Button>
            )}
          </DialogTitle>
        </DialogHeader>

        {editingId !== null ? (
          <form onSubmit={form.handleSubmit(handleSave)} className="space-y-4 py-2">
            <div>
              <Label className="text-slate-300">{t("Shards.CollectionName")}</Label>
              <Input
                {...form.register("name", { required: true })}
                placeholder="e.g. Minecraft, Source Engine, Discord Bots"
                className="mt-1.5 bg-slate-900 border-white/10"
              />
            </div>
            <div>
              <Label className="text-slate-300">{t("Shards.CollectionAuthor")}</Label>
              <Input
                {...form.register("author")}
                placeholder="e.g. Midnightpanel or community@example.com"
                className="mt-1.5 bg-slate-900 border-white/10"
              />
            </div>
            <div>
              <Label className="text-slate-300">{t("Common.Description")}</Label>
              <Textarea
                {...form.register("description")}
                placeholder="Optional description of this collection..."
                className="mt-1.5 bg-slate-900 border-white/10 resize-none h-20"
              />
            </div>
            <DialogFooter className="pt-2">
              <Button type="button" variant="outline" onClick={handleCancelForm} disabled={busy}>
                {t("Common.Cancel")}
              </Button>
              <Button type="submit" disabled={busy} className="bg-indigo-600 hover:bg-indigo-500">
                {t("Common.Save")}
              </Button>
            </DialogFooter>
          </form>
        ) : (
          <div className="space-y-3 max-h-[420px] overflow-y-auto pr-1">
            {isLoading ? (
              <div className="py-8 text-center text-slate-400">Loading collections...</div>
            ) : collections.length === 0 ? (
              <div className="rounded-xl border border-dashed border-white/10 p-8 text-center text-slate-400">
                No shard collections found. Click &quot;Create Collection&quot;.
              </div>
            ) : (
              collections.map((col) => (
                <div
                  key={col.id}
                  className="flex items-center justify-between rounded-xl border border-white/5 bg-slate-900/60 p-4 transition-colors hover:border-white/10"
                >
                  <div className="space-y-1">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-white">{col.name}</span>
                      <Badge size="sm" variant="light" color="primary" className="text-xs">
                        {col.shardCount} Shards
                      </Badge>
                    </div>
                    {col.description && <p className="text-xs text-slate-400">{col.description}</p>}
                    <span className="text-[11px] text-slate-500">Author: {col.author}</span>
                  </div>
                  <div className="flex items-center gap-1.5">
                    <Button variant="ghost" size="icon-sm" onClick={() => handleStartEdit(col)}>
                      <PencilIcon className="size-4 text-slate-400 hover:text-white" />
                    </Button>
                    <Button variant="ghost" size="icon-sm" onClick={() => handleDelete(col)}>
                      <TrashBinIcon className="size-4 text-rose-400 hover:text-rose-300" />
                    </Button>
                  </div>
                </div>
              ))
            )}
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
