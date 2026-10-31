import { shardsApi } from "@/api/shards";
import type { ShardCollection, ValidateVariablesResponse } from "@/api/types";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { useT } from "@/i18n";
import { BoxIcon, CheckCircleIcon, SparkIcon } from "@/icons";
import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";

interface ShardSelectorProps {
  onSelectionChange?: (data: {
    shardId: number;
    dockerImage: string;
    variables: Record<string, string>;
    isValid: boolean;
  }) => void;
}

export function ShardSelector({ onSelectionChange }: ShardSelectorProps) {
  const { t } = useT();

  const [selectedCollectionId, setSelectedCollectionId] = useState<number | undefined>(undefined);
  const [selectedShardId, setSelectedShardId] = useState<number | null>(null);
  const [selectedDockerImage, setSelectedDockerImage] = useState<string>("");
  const [variableValues, setVariableValues] = useState<Record<string, string>>({});
  const [validationResult, setValidationResult] = useState<ValidateVariablesResponse | null>(null);
  const [isValidating, setIsValidating] = useState(false);

  // Load collections
  const { data: collections = [] } = useQuery({
    queryKey: ["user", "shards", "collections"],
    queryFn: () => shardsApi.listAllCollections(),
  });

  // Load shards for selected collection
  const { data: shards = [], isLoading: isShardsLoading } = useQuery({
    queryKey: ["user", "shards", selectedCollectionId],
    queryFn: () => shardsApi.listUserShards(selectedCollectionId),
  });

  // Load selected shard details
  const { data: shardDetail } = useQuery({
    queryKey: ["user", "shard", selectedShardId],
    queryFn: () => (selectedShardId ? shardsApi.getUserShard(selectedShardId) : null),
    enabled: selectedShardId !== null,
  });

  // Initialize default values when shard changes
  useEffect(() => {
    if (shardDetail) {
      setSelectedDockerImage(shardDetail.defaultDockerImage || Object.values(shardDetail.dockerImages)[0] || "");

      const initialValues: Record<string, string> = {};
      for (const v of shardDetail.variables) {
        initialValues[v.envVariable] = v.defaultValue || "";
      }
      setVariableValues(initialValues);
    }
  }, [shardDetail]);

  // Live validation of variables
  useEffect(() => {
    if (!selectedShardId || !shardDetail) return;

    const timer = setTimeout(async () => {
      setIsValidating(true);
      try {
        const res = await shardsApi.validateVariables(selectedShardId, {
          values: variableValues,
        });
        setValidationResult(res);

        onSelectionChange?.({
          shardId: selectedShardId,
          dockerImage: selectedDockerImage,
          variables: variableValues,
          isValid: res.isValid,
        });
      } catch {
        // Fallback
      } finally {
        setIsValidating(false);
      }
    }, 250);

    return () => clearTimeout(timer);
  }, [selectedShardId, shardDetail, variableValues, selectedDockerImage, onSelectionChange]);

  const handleVariableChange = (envVar: string, val: string) => {
    setVariableValues((prev) => ({ ...prev, [envVar]: val }));
  };

  return (
    <div className="space-y-6">
      {/* Step 1: Select Collection & Shard */}
      <Card className="p-5 lg:p-6 space-y-5">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <h3 className="text-base font-bold text-gray-800 dark:text-white/90 flex items-center gap-2">
              <span className="flex size-6 items-center justify-center rounded-full bg-brand-500 text-white text-xs font-semibold">1</span>
              {t("Shards.Selector.ChooseShard")}
            </h3>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Select server type and runtime template.</p>
          </div>

          {/* Collection Filter Tabs / Select */}
          <div className="flex items-center gap-2">
            <select
              value={selectedCollectionId ? String(selectedCollectionId) : ""}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) => {
                setSelectedCollectionId(e.target.value ? Number(e.target.value) : undefined);
                setSelectedShardId(null);
              }}
              className="flex h-10 rounded-[10px] border border-gray-300 bg-white px-3 py-1.5 text-xs text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 min-w-[160px]"
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

        {/* Shards Cards */}
        {isShardsLoading ? (
          <div className="py-8 text-center text-xs text-gray-500 dark:text-gray-400">Loading shards...</div>
        ) : shards.length === 0 ? (
          <div className="rounded-xl border border-dashed border-gray-200 p-8 text-center text-xs text-gray-500 dark:border-gray-800 dark:text-gray-400">
            No shards available in this collection yet.
          </div>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 gap-3 pt-1">
            {shards.map((s) => {
              const isSelected = selectedShardId === s.id;
              return (
                <button
                  key={s.id}
                  type="button"
                  onClick={() => setSelectedShardId(s.id)}
                  className={`flex flex-col text-left p-4 rounded-xl border transition-all duration-200 cursor-pointer ${
                    isSelected
                      ? "border-brand-500 bg-brand-50/50 dark:bg-brand-500/10 shadow-sm ring-1 ring-brand-500"
                      : "border-gray-200 bg-white hover:border-gray-300 hover:bg-gray-50/50 dark:border-gray-800 dark:bg-white/[0.02] dark:hover:border-gray-700 dark:hover:bg-white/5"
                  }`}
                >
                  <div className="flex items-center justify-between w-full mb-2">
                    <div className="flex items-center gap-2.5">
                      <div
                        className={`flex size-8 items-center justify-center rounded-lg ${
                          isSelected
                            ? "bg-brand-500 text-white"
                            : "bg-brand-50 text-brand-500 dark:bg-brand-500/10 dark:text-brand-400"
                        }`}
                      >
                        <BoxIcon className="size-4" />
                      </div>
                      <span className="font-semibold text-sm text-gray-800 dark:text-white/90">{s.name}</span>
                    </div>
                    {isSelected && <CheckCircleIcon className="size-4 text-brand-500" />}
                  </div>

                  <p className="text-xs text-gray-500 dark:text-gray-400 line-clamp-2 min-h-[32px]">
                    {s.description || "No description available."}
                  </p>

                  <div className="mt-3 flex items-center justify-between text-[11px] text-gray-400 dark:text-gray-500 border-t border-gray-100 dark:border-white/5 pt-2">
                    <span>{s.collectionName}</span>
                    <span>v{s.version}</span>
                  </div>
                </button>
              );
            })}
          </div>
        )}
      </Card>

      {/* Step 2: Select Docker Image */}
      {shardDetail && (
        <Card className="p-5 lg:p-6 space-y-4">
          <div>
            <h3 className="text-base font-bold text-gray-800 dark:text-white/90 flex items-center gap-2">
              <span className="flex size-6 items-center justify-center rounded-full bg-brand-500 text-white text-xs font-semibold">2</span>
              {t("Shards.Tabs.DockerImages")}
            </h3>
            <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">Select the container image for your server version.</p>
          </div>

          <div className="max-w-md">
            <select
              value={selectedDockerImage}
              onChange={(e: React.ChangeEvent<HTMLSelectElement>) => setSelectedDockerImage(e.target.value)}
              className="flex h-11 w-full rounded-[10px] border border-gray-300 bg-white px-3 py-2 text-xs text-gray-800 shadow-theme-xs outline-hidden transition-colors focus:border-brand-300 focus:ring-3 focus:ring-brand-500/20 dark:border-gray-700 dark:bg-gray-900 dark:text-white/90 font-mono"
            >
              {Object.entries(shardDetail.dockerImages || {}).map(([label, uri]) => (
                <option key={label} value={uri}>
                  {label} ({uri})
                </option>
              ))}
            </select>
          </div>
        </Card>
      )}

      {/* Step 3: Dynamic Variables */}
      {shardDetail && (
        <Card className="p-5 lg:p-6 space-y-5">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-2">
            <div>
              <h3 className="text-base font-bold text-gray-800 dark:text-white/90 flex items-center gap-2">
                <span className="flex size-6 items-center justify-center rounded-full bg-brand-500 text-white text-xs font-semibold">3</span>
                {t("Shards.Selector.ConfigureVariables")}
              </h3>
              <p className="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                Configure variables for your server. Required fields are marked.
              </p>
            </div>
            {isValidating && (
              <span className="text-xs text-brand-500 animate-pulse">{t("Shards.Selector.Validating")}</span>
            )}
          </div>

          {shardDetail.variables.length === 0 ? (
            <p className="text-xs text-gray-500 dark:text-gray-400 py-3 text-center border border-dashed border-gray-200 dark:border-gray-800 rounded-xl">
              This shard does not require any custom variables.
            </p>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {shardDetail.variables.map((variable) => {
                const isRequired = variable.rules.includes("required");
                const isBoolean = variable.rules.includes("boolean");
                const isNumeric = variable.rules.includes("numeric") || variable.rules.includes("integer");
                const error = validationResult?.errors?.[variable.envVariable];
                const currentVal = variableValues[variable.envVariable] ?? variable.defaultValue ?? "";

                return (
                  <div
                    key={variable.id}
                    className="rounded-xl border border-gray-200 bg-gray-50/60 p-4 space-y-2 transition-colors hover:border-gray-300 dark:border-gray-800 dark:bg-white/[0.02] dark:hover:border-gray-700"
                  >
                    <div className="flex items-center justify-between">
                      <Label className="text-xs font-medium text-gray-800 dark:text-white/90 flex items-center gap-1">
                        {variable.name}
                        {isRequired && <span className="text-rose-500 font-bold">*</span>}
                      </Label>
                      <Badge size="sm" variant="light" color="dark" className="font-mono text-[10px]">
                        {variable.envVariable}
                      </Badge>
                    </div>

                    {isBoolean ? (
                      <div className="flex items-center gap-3 pt-1">
                        <Switch
                          checked={currentVal === "true" || currentVal === "1"}
                          onCheckedChange={(c) => handleVariableChange(variable.envVariable, c ? "true" : "false")}
                          disabled={!variable.userEditable}
                        />
                        <span className="text-xs text-gray-500 dark:text-gray-400 font-mono">
                          {currentVal === "true" || currentVal === "1" ? "Enabled (true)" : "Disabled (false)"}
                        </span>
                      </div>
                    ) : (
                      <Input
                        type={isNumeric ? "number" : "text"}
                        value={currentVal}
                        onChange={(e) => handleVariableChange(variable.envVariable, e.target.value)}
                        disabled={!variable.userEditable}
                        className={`font-mono text-xs ${
                          error ? "border-rose-500 focus:ring-rose-500/20" : ""
                        }`}
                        placeholder={variable.defaultValue || ""}
                      />
                    )}

                    {variable.description && <p className="text-[11px] text-gray-500 dark:text-gray-400">{variable.description}</p>}
                    {error && <p className="text-[11px] text-rose-500 font-medium">{error}</p>}
                  </div>
                );
              })}
            </div>
          )}

          {/* Startup Command Live Preview */}
          {validationResult?.resolvedStartupCommand && (
            <div className="rounded-xl border border-brand-200 bg-brand-50/50 p-4 space-y-2 dark:border-brand-500/20 dark:bg-brand-950/20">
              <div className="flex items-center gap-2 text-xs font-semibold text-brand-600 dark:text-brand-400">
                <SparkIcon className="size-4" />
                {t("Shards.Selector.StartupPreview")}
              </div>
              <pre className="font-mono text-xs text-gray-800 dark:text-gray-200 bg-white dark:bg-black/40 p-3 rounded-lg overflow-x-auto whitespace-pre-wrap leading-relaxed border border-gray-200 dark:border-white/5">
                {validationResult.resolvedStartupCommand}
              </pre>
            </div>
          )}
        </Card>
      )}
    </div>
  );
}
