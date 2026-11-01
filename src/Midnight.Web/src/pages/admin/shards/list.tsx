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
import { useEffect, useState } from "react";
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

  // Load collections for filtering
  const { data: collections = [] } = useQuery({
    queryKey: ["admin", "shards", "collections", "all"],
    queryFn: () => shardsApi.listAllCollections(),
  });

  // Load shards
  const query: PagedQuery = {
    page,
    pageSize,
    search: search.trim() || undefined,
  };

  const { data: pagedShards, isLoading } = useQuery({
    queryKey: ["admin", "shards", "list", query, selectedCollectionId],
    queryFn: () => shardsApi.listShards(query, selectedCollectionId),
  });

  // Form for new Shard
  const createForm = useForm<ShardRequest>({
    defaultValues: {
      collectionId: 0,
      name: "",
      author: "Midnightpanel",
      version: "1.0.0",
      description: "",
      startupCommand: "java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}",
      defaultDockerImage: "ghcr.io/midnight-panel/java:21",
      dockerImages: { "Java 21": "ghcr.io/midnight-panel/java:21" },
      configFiles: [],
      features: { eula: true, fileDenylist: [] },
    },
  });

  // The collections arrive after the form was created, preselect the first one like the select shows it
  useEffect(() => {
    if (collections[0] && !collections.some((c) => c.id === Number(createForm.getValues("collectionId"))))
      createForm.setValue("collectionId", collections[0].id);
  }, [collections, createForm]);

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
      <PageHeader icon={<BoxIcon />} title={t("Shards.Title")} description={t("Shards.Description")} />

      {/* Toolbar / Actions */}
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        {/* Search & Filter */}
        <div className="flex flex-1 flex-wrap items-center gap-3">
          <div className="relative min-w-[240px] flex-1 sm:max-w-xs">
            <SearchIcon className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-gray-400" />
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
              placeholder="Search shards..."
              className="pl-9"
            />
          </div>

          <div className="flex items-center gap-2">
            <FilterIcon className="size-4 text-gray-400" />
            <select
              value={selectedCollectionId ? String(selectedCollectionId) : ""}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                setSelectedCollectionId(e.target.value ? Number(e.target.value) : undefined);
                setPage(1);
              }}
              className="flex h-11 rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 min-w-[180px]"
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
            className="w-full sm:w-auto"
          >
            <FolderIcon className="size-4 mr-1.5 text-brand-500" />
            {t("Shards.Collections")}
          </Button>

          {can(Permissions.AdminShardsWrite) && (
            <>
              <Button
                variant="outline"
                onClick={() => setImportModalOpen(true)}
                className="w-full sm:w-auto"
              >
                <UploadIcon className="size-4 mr-1.5 text-brand-500" />
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
                    defaultDockerImage: "ghcr.io/midnight-panel/java:21",
                    dockerImages: { "Java 21": "ghcr.io/midnight-panel/java:21" },
                    configFiles: [],
                    features: { eula: true, fileDenylist: [] },
                  });
                  setCreateModalOpen(true);
                }}
                className="w-full sm:w-auto"
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
        <div className="flex h-64 items-center justify-center text-gray-500 dark:text-gray-400">Loading shards...</div>
      ) : !pagedShards || pagedShards.items.length === 0 ? (
        <div className="rounded-2xl border border-dashed border-gray-200 bg-gray-50/50 p-12 text-center dark:border-gray-800 dark:bg-white/[0.02]">
          <BoxIcon className="mx-auto size-12 text-gray-400 mb-3 opacity-60" />
          <h3 className="text-base font-semibold text-gray-800 dark:text-white/90">No shards found</h3>
          <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">
            Create your first shard or import an existing template.
          </p>
        </div>
      ) : (
        <div className="space-y-6">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {pagedShards.items.map((shard) => (
              <div
                key={shard.id}
                className="group relative flex flex-col justify-between rounded-2xl border border-gray-200 bg-white p-5 shadow-theme-xs transition-all duration-200 hover:border-brand-300 hover:shadow-md dark:border-gray-800 dark:bg-white/[0.02] dark:hover:border-brand-500/40"
              >
                {/* Header */}
                <div>
                  <div className="flex items-start justify-between gap-3">
                    <div className="flex items-center gap-3">
                      <div className="flex size-11 items-center justify-center rounded-xl bg-brand-50 text-brand-500 border border-brand-100 group-hover:bg-brand-500 group-hover:text-white transition-all dark:bg-brand-500/10 dark:text-brand-400 dark:border-brand-500/20">
                        <BoxIcon className="size-6" />
                      </div>
                      <div>
                        <Link
                          to={`/admin/shards/${shard.id}`}
                          className="font-semibold text-gray-800 dark:text-white/90 hover:text-brand-500 dark:hover:text-brand-400 transition-colors line-clamp-1"
                        >
                          {shard.name}
                        </Link>
                        <span className="text-xs text-gray-500 dark:text-gray-400 flex items-center gap-1.5 mt-0.5">
                          <span className="text-brand-500 dark:text-brand-400 font-medium">{shard.collectionName}</span>
                          <span>•</span>
                          <span>v{shard.version}</span>
                        </span>
                      </div>
                    </div>
                  </div>

                  {/* Description */}
                  <p className="mt-3 text-xs text-gray-500 dark:text-gray-400 line-clamp-2 min-h-[32px]">
                    {shard.description || "No description provided."}
                  </p>
                </div>

                {/* Footer Info & Actions */}
                <div className="mt-5 border-t border-gray-100 dark:border-white/5 pt-3.5">
                  <div className="flex items-center justify-between text-xs">
                    <div className="flex items-center gap-2">
                      <Badge variant="light" color="primary" size="sm">
                        {shard.variableCount} Variables
                      </Badge>
                    </div>

                    <div className="flex items-center gap-1">
                      <Button
                        variant="ghost"
                        size="icon-sm"
                        onClick={() => handleExport(shard)}
                        title={t("Shards.Export")}
                        className="text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white"
                      >
                        <DownloadIcon className="size-4" />
                      </Button>

                      {can(Permissions.AdminShardsWrite) && (
                        <Button
                          variant="ghost"
                          size="icon-sm"
                          onClick={() => handleDelete(shard)}
                          title={t("Shards.Delete")}
                          className="text-rose-500 hover:text-rose-600 dark:text-rose-400"
                        >
                          <TrashBinIcon className="size-4" />
                        </Button>
                      )}

                      <Button
                        variant="ghost"
                        size="icon-sm"
                        render={<Link to={`/admin/shards/${shard.id}`} />}
                        nativeButton={false}
                        className="text-gray-500 hover:text-gray-800 dark:text-gray-400 dark:hover:text-white ml-1"
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
            <div className="flex flex-col sm:flex-row items-center justify-between gap-3 pt-2 text-xs text-gray-500 dark:text-gray-400">
              <span>
                Page {page} of {pagedShards.totalPages} ({pagedShards.totalCount} shards)
              </span>
              <div className="flex items-center gap-2">
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  disabled={page >= pagedShards.totalPages}
                  onClick={() => setPage((p) => Math.min(pagedShards.totalPages, p + 1))}
                >
                  Next
                </Button>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Create Shard Dialog */}
      <Dialog open={createModalOpen} onOpenChange={setCreateModalOpen}>
        <DialogContent className="sm:max-w-xl">
          <DialogHeader>
            <DialogTitle>{t("Shards.Create")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={createForm.handleSubmit(handleCreate)} className="space-y-4 py-2">
            <div>
              <Label>{t("Shards.General.Collection")}</Label>
              <select
                {...createForm.register("collectionId", { required: true })}
                className="mt-1.5 flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90"
              >
                {collections.map((c: ShardCollection) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div>
                <Label>{t("Shards.General.Name")}</Label>
                <Input
                  {...createForm.register("name", { required: true })}
                  placeholder="e.g. Paper, CS2, Rust"
                  className="mt-1.5"
                />
              </div>
              <div>
                <Label>{t("Shards.General.Version")}</Label>
                <Input
                  {...createForm.register("version")}
                  placeholder="1.0.0"
                  className="mt-1.5"
                />
              </div>
            </div>

            <div>
              <Label>{t("Shards.General.Author")}</Label>
              <Input
                {...createForm.register("author")}
                placeholder="e.g. team@midnightpanel.io"
                className="mt-1.5"
              />
            </div>

            <div>
              <Label>{t("Common.Description")}</Label>
              <Textarea
                {...createForm.register("description")}
                placeholder="Brief description of the shard..."
                className="mt-1.5 resize-none h-18"
              />
            </div>

            <div>
              <Label>{t("Shards.Config.StartupCommand")}</Label>
              <Input
                {...createForm.register("startupCommand", { required: true })}
                placeholder="java -Xmx{{SERVER_MEMORY}}M -jar {{SERVER_JARFILE}}"
                className="mt-1.5 font-mono text-xs"
              />
            </div>

            <DialogFooter className="pt-2">
              <Button type="button" variant="outline" onClick={() => setCreateModalOpen(false)}>
                {t("Common.Cancel")}
              </Button>
              <Button type="submit" disabled={busy}>
                {busy ? "Creating..." : t("Shards.Create")}
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
