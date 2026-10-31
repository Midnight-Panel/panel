import { shardsApi } from "@/api/shards";
import type { PagedQuery, ShardCollection, ShardRequest, ShardSummary } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { RequirePermission } from "@/components/shared/guards";
import { PageHeader } from "@/components/shared/page-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { useT } from "@/i18n";
import {
  BoxIcon,
  ChevronRightIcon,
  DownloadIcon,
  FilterIcon,
  FolderIcon,
  PlusIcon,
  SearchIcon,
  TrashBinIcon,
  UploadIcon,
} from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { CollectionsModal } from "./collections-modal";
import { ImportModal } from "./import-modal";

export default function ShardsPage() {
  const { t } = useT();
  const { can } = useAuth();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const [search, setSearch] = useState("");
  const [selectedCollectionId, setSelectedCollectionId] = useState<number | undefined>(undefined);
  const [page, setPage] = useState(1);
  const pageSize = 12;

  const [collectionsModalOpen, setCollectionsModalOpen] = useState(false);
  const [importModalOpen, setImportModalOpen] = useState(false);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [busy, setBusy] = useState(false);

  // Sammlungen laden für Filter
  const { data: collections = [] } = useQuery({
    queryKey: ["admin", "shards", "collections", "all"],
    queryFn: () => shardsApi.listAllCollections(),
  });

  // Shards laden
  const query: PagedQuery = {
    page,
    pageSize,
    search: search.trim() || undefined,
  };

  const { data: pagedShards, isLoading } = useQuery({
    queryKey: ["admin", "shards", "list", query, selectedCollectionId],
    queryFn: () => shardsApi.listShards(query, selectedCollectionId),
  });

  // Formular für neuen Shard
  const createForm = useForm<ShardRequest>({
    defaultValues: {
      collectionId: collections[0]?.id || 1,
      name: "",
      author: "Midnightpanel",
      version: "1.0.0",
      description: "",
      startupCommand: "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}",
      defaultDockerImage: "ghcr.io/midnightpanel/yolks:java_21",
      dockerImages: { "Java 21": "ghcr.io/midnightpanel/yolks:java_21" },
      configFiles: [],
      features: { eula: true, fileDenylist: [] },
    },
  });

  const handleCreate = async (values: ShardRequest) => {
    setBusy(true);
    try {
      const newShard = await shardsApi.createShard({
        ...values,
        collectionId: Number(values.collectionId),
      });
      await queryClient.invalidateQueries({ queryKey: ["admin", "shards"] });
      setCreateModalOpen(false);
      notifySuccess(t("Shards.Created", newShard.name));
      navigate(`/admin/shards/${newShard.id}`);
    } catch (err) {
      notifyError(err);
    } finally {
      setBusy(false);
    }
  };

  const handleDelete = async (shard: ShardSummary) => {
    if (!confirm(t("Shards.DeleteConfirm", shard.name))) return;
    try {
      await shardsApi.deleteShard(shard.id);
      notifySuccess(t("Shards.Deleted"));
      await queryClient.invalidateQueries({ queryKey: ["admin", "shards"] });
    } catch (err) {
      notifyError(err);
    }
  };

  const handleExport = async (shard: ShardSummary) => {
    try {
      await shardsApi.exportShard(shard.id, shard.name);
      notifySuccess(t("Shards.Export"));
    } catch (err) {
      notifyError(err);
    }
  };

  return (
    <RequirePermission permission={Permissions.AdminShardsRead}>
      <PageHeader title={t("Shards.Title")} description={t("Shards.Description")} />

      {/* Toolbar / Actions */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        {/* Search & Filter */}
        <div className="flex flex-1 flex-wrap items-center gap-3">
          <div className="relative min-w-[240px] flex-1 sm:max-w-xs">
            <SearchIcon className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-slate-400" />
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder="Shards durchsuchen..."
              className="pl-9 bg-[#0F172A] border-white/10 text-white"
            />
          </div>

          <div className="flex items-center gap-2">
            <FilterIcon className="size-4 text-slate-400" />
            <select
              value={selectedCollectionId ? String(selectedCollectionId) : ""}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                setSelectedCollectionId(e.target.value ? Number(e.target.value) : undefined);
                setPage(1);
              }}
              className="flex h-10 rounded-lg border border-white/10 bg-[#0F172A] px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500 min-w-[180px]"
            >
              <option value="">{t("Shards.AllCollections")}</option>
              {collections.map((c: ShardCollection) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex flex-wrap items-center gap-2.5">
          <Button
            variant="outline"
            onClick={() => setCollectionsModalOpen(true)}
            className="border-white/10 bg-slate-900/60 hover:bg-slate-800 text-slate-200"
          >
            <FolderIcon className="size-4 mr-1.5 text-indigo-400" />
            {t("Shards.Collections")}
          </Button>

          {can(Permissions.AdminShardsWrite) && (
            <>
              <Button
                variant="outline"
                onClick={() => setImportModalOpen(true)}
                className="border-white/10 bg-slate-900/60 hover:bg-slate-800 text-slate-200"
              >
                <UploadIcon className="size-4 mr-1.5 text-indigo-400" />
                {t("Shards.Import")}
              </Button>

              <Button
                onClick={() => {
                  createForm.reset({
                    collectionId: collections[0]?.id || 1,
                    name: "",
                    author: "Midnightpanel",
                    version: "1.0.0",
                    description: "",
                    startupCommand: "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}",
                    defaultDockerImage: "ghcr.io/midnightpanel/yolks:java_21",
                    dockerImages: { "Java 21": "ghcr.io/midnightpanel/yolks:java_21" },
                    configFiles: [],
                    features: { eula: true, fileDenylist: [] },
                  });
                  setCreateModalOpen(true);
                }}
                className="bg-indigo-600 hover:bg-indigo-500 text-white shadow-lg shadow-indigo-600/30"
              >
                <PlusIcon className="size-4 mr-1.5" />
                {t("Shards.Create")}
              </Button>
            </>
          )}
        </div>
      </div>

      {/* Shard Grid */}
      {isLoading ? (
        <div className="flex h-64 items-center justify-center text-slate-400">Lade Shards...</div>
      ) : !pagedShards || pagedShards.items.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-white/10 bg-[#0F172A]/40 p-12 text-center">
          <BoxIcon className="mx-auto size-12 text-slate-500 mb-3 opacity-60" />
          <h3 className="text-base font-semibold text-white">Keine Shards gefunden</h3>
          <p className="mt-1 text-sm text-slate-400">
            Erstelle deinen ersten Shard oder importiere eine bestehende Vorlage.
          </p>
        </div>
      ) : (
        <div className="space-y-6">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {pagedShards.items.map((shard) => (
              <div
                key={shard.id}
                className="group relative flex flex-col justify-between rounded-2xl border border-white/10 bg-[#0F172A] p-5 shadow-xl transition-all duration-200 hover:border-indigo-500/40 hover:shadow-indigo-500/10 hover:shadow-2xl"
              >
                {/* Header */}
                <div>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className="flex size-11 items-center justify-center rounded-xl bg-indigo-500/10 text-indigo-400 border border-indigo-500/20 shadow-inner group-hover:bg-indigo-500 group-hover:text-white transition-all">
                        <BoxIcon className="size-6" />
                      </div>
                      <div>
                        <Link
                          to={`/admin/shards/${shard.id}`}
                          className="font-semibold text-white hover:text-indigo-400 transition-colors line-clamp-1"
                        >
                          {shard.name}
                        </Link>
                        <span className="text-xs text-slate-400 flex items-center gap-1.5 mt-0.5">
                          <span className="text-indigo-400 font-medium">{shard.collectionName}</span>
                          <span>•</span>
                          <span>v{shard.version}</span>
                        </span>
                      </div>
                    </div>
                  </div>

                  {/* Description */}
                  <p className="mt-3 text-xs text-slate-400 line-clamp-2 min-h-[32px]">
                    {shard.description || "Keine Beschreibung hinterlegt."}
                  </p>
                </div>

                {/* Footer Info & Actions */}
                <div className="mt-5 border-t border-white/5 pt-3.5">
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-2">
                      <Badge variant="light" color="primary" size="sm">
                        {shard.variableCount} Variablen
                      </Badge>
                    </div>

                    <div className="flex items-center gap-1">
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        onClick={() => handleExport(shard)}
                        title={t("Shards.Export")}
                        className="text-slate-400 hover:text-white"
                      >
                        <DownloadIcon className="size-4" />
                      </Button>

                      {can(Permissions.AdminShardsWrite) && (
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          onClick={() => handleDelete(shard)}
                          title={t("Shards.Delete")}
                          className="text-rose-400 hover:text-rose-300"
                        >
                          <TrashBinIcon className="size-4" />
                        </Button>
                      )}

                      <Button
                        variant="ghost"
                        size="icon-sm"
                        render={<Link to={`/admin/shards/${shard.id}`} />}
                        nativeButton={false}
                        className="text-slate-400 hover:text-white ml-1"
                      >
                        <ChevronRightIcon className="size-4" />
                      </Button>
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>

          {/* Pagination */}
          {pagedShards.totalPages > 1 && (
            <div className="flex items-center justify-between pt-2 text-xs text-slate-400">
              <span>
                Seite {page} von {pagedShards.totalPages} ({pagedShards.totalCount} Shards)
              </span>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                  className="border-white/10"
                >
                  Zurück
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= pagedShards.totalPages}
                  onClick={() => setPage((p) => Math.min(pagedShards.totalPages, p + 1))}
                  className="border-white/10"
                >
                  Weiter
                </Button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Create Shard Dialog */}
      <Dialog open={createModalOpen} onOpenChange={setCreateModalOpen}>
        <DialogContent className="sm:max-w-xl bg-[#0F172A] border-white/10 text-white">
          <DialogHeader>
            <DialogTitle className="text-xl font-bold">{t("Shards.Create")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={createForm.handleSubmit(handleCreate)} className="space-y-4 py-2">
            <div>
              <Label className="text-slate-300">{t("Shards.General.Collection")}</Label>
              <select
                {...createForm.register("collectionId", { required: true })}
                className="mt-1.5 flex h-10 w-full rounded-lg border border-white/10 bg-slate-900 px-3 py-2 text-sm text-white focus:outline-none focus:ring-2 focus:ring-indigo-500"
              >
                {collections.map((c: ShardCollection) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div>
                <Label className="text-slate-300">{t("Shards.General.Name")}</Label>
                <Input
                  {...createForm.register("name", { required: true })}
                  placeholder="z. B. Paper, CS2, Rust"
                  className="mt-1.5 bg-slate-900 border-white/10"
                />
              </div>
              <div>
                <Label className="text-slate-300">{t("Shards.General.Version")}</Label>
                <Input
                  {...createForm.register("version")}
                  placeholder="1.0.0"
                  className="mt-1.5 bg-slate-900 border-white/10"
                />
              </div>
            </div>

            <div>
              <Label className="text-slate-300">{t("Shards.General.Author")}</Label>
              <Input
                {...createForm.register("author")}
                placeholder="z. B. team@midnightpanel.io"
                className="mt-1.5 bg-slate-900 border-white/10"
              />
            </div>

            <div>
              <Label className="text-slate-300">{t("Common.Description")}</Label>
              <Textarea
                {...createForm.register("description")}
                placeholder="Kurzbeschreibung des Shards..."
                className="mt-1.5 bg-slate-900 border-white/10 resize-none h-18"
              />
            </div>

            <div>
              <Label className="text-slate-300">{t("Shards.Config.StartupCommand")}</Label>
              <Input
                {...createForm.register("startupCommand", { required: true })}
                placeholder="java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}"
                className="mt-1.5 font-mono text-xs bg-slate-900 border-white/10"
              />
            </div>

            <DialogFooter className="pt-2">
              <Button type="button" variant="outline" onClick={() => setCreateModalOpen(false)}>
                {t("Common.Cancel")}
              </Button>
              <Button type="submit" disabled={busy} className="bg-indigo-600 hover:bg-indigo-500">
                {busy ? "Erstelle..." : t("Shards.Create")}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Modals */}
      <CollectionsModal open={collectionsModalOpen} onOpenChange={setCollectionsModalOpen} />
      <ImportModal
        open={importModalOpen}
        onOpenChange={setImportModalOpen}
        onSuccess={() => queryClient.invalidateQueries({ queryKey: ["admin", "shards"] })}
      />
    </RequirePermission>
  );
}
