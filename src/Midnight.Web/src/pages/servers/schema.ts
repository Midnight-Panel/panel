import type { ServerVariable, ShardVariable } from "@/api/types";
import { problemFieldErrors, threadsPattern, validateVariable } from "@/lib/servers";
import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import * as z from "zod";

type Translate = (key: string, ...args: Array<string | number>) => string;

/** Integer field matching a [Range(min, max)] of the C# DTOs */
export function rangeField(t: Translate, min: number, max: number) {
  const message = t("Servers.Validation.Range", min, max);
  return z.number({ invalid_type_error: message, required_error: message }).int(message).min(min, message).max(max, message);
}

/** ServerLimitsDto */
export function limitsSchema(t: Translate) {
  return z.object({
    cpu: rangeField(t, 0, 100_000),
    memory: rangeField(t, 64, 10_485_760),
    swap: rangeField(t, -1, 10_485_760),
    disk: rangeField(t, 0, 104_857_600),
    ioWeight: rangeField(t, 10, 1000),
    threads: z.string().max(64, t("Servers.Validation.Threads")).regex(threadsPattern, t("Servers.Validation.Threads")),
    oomKillerDisabled: z.boolean(),
  });
}

/** ServerFeatureLimitsDto */
export function featureLimitsSchema(t: Translate) {
  return z.object({
    databases: rangeField(t, 0, 1000),
    allocations: rangeField(t, 0, 1000),
    backups: rangeField(t, 0, 1000),
  });
}

/** One variable in a form, the definition travels along so the rules can be checked */
export interface VariableFormValue {
  envVariable: string;
  name: string;
  description?: string | null;
  defaultValue?: string | null;
  rules: string;
  editable: boolean;
  value: string;
}

export function variablesSchema(t: Translate) {
  return z
    .array(
      z.object({
        envVariable: z.string(),
        name: z.string(),
        description: z.string().nullish(),
        defaultValue: z.string().nullish(),
        rules: z.string(),
        editable: z.boolean(),
        value: z.string(),
      }),
    )
    .superRefine((variables, context) => {
      variables.forEach((variable, index) => {
        if (!variable.editable) return;

        const error = validateVariable(variable.value, variable.rules, t);
        if (error) context.addIssue({ code: z.ZodIssueCode.custom, message: error, path: [index, "value"] });
      });
    });
}

export function variablesFromShard(variables: ShardVariable[]): VariableFormValue[] {
  return [...variables]
    .sort((a, b) => a.sortOrder - b.sortOrder)
    .map((variable) => ({
      envVariable: variable.envVariable,
      name: variable.name,
      description: variable.description,
      defaultValue: variable.defaultValue,
      rules: variable.rules,
      editable: true,
      value: variable.defaultValue ?? "",
    }));
}

/** Server variables, `editable` decides which ones the current user may change */
export function variablesFromServer(variables: ServerVariable[], editable: (variable: ServerVariable) => boolean): VariableFormValue[] {
  return variables.map((variable) => ({
    envVariable: variable.envVariable,
    name: variable.name,
    description: variable.description,
    defaultValue: variable.defaultValue,
    rules: variable.rules,
    editable: editable(variable),
    value: variable.value ?? variable.defaultValue ?? "",
  }));
}

/** EnvVariable => value of the editable variables */
export function variablesToRecord(variables: VariableFormValue[]) {
  return Object.fromEntries(variables.filter((variable) => variable.editable).map((variable) => [variable.envVariable, variable.value]));
}

/** Api keys of ServerLimitsDto / ServerFeatureLimitsDto => form paths */
export const limitsErrorPaths = {
  "Limits.Cpu": "limits.cpu",
  "Limits.Memory": "limits.memory",
  "Limits.Swap": "limits.swap",
  "Limits.Disk": "limits.disk",
  "Limits.IoWeight": "limits.ioWeight",
  "Limits.Threads": "limits.threads",
  "FeatureLimits.Databases": "featureLimits.databases",
  "FeatureLimits.Allocations": "featureLimits.allocations",
  "FeatureLimits.Backups": "featureLimits.backups",
} as const;

/** Form paths of the variables, keyed by EnvVariable like the "errors" of the api */
export function variableErrorPaths(variables: VariableFormValue[]) {
  return Object.fromEntries(variables.map((variable, index) => [variable.envVariable, `variables.${index}.value` as const]));
}

/**
 * Shows the field errors of a problem response at the matching inputs. `paths` maps api keys to form paths:
 * exact keys first (EnvVariables), then case insensitive ("Limits.Cpu" => "limits.cpu").
 * Returns how many errors could be placed.
 */
export function applyProblemErrors<T extends FieldValues>(setError: UseFormSetError<T>, error: unknown, paths: Record<string, Path<T>>) {
  const lower = new Map(Object.entries(paths).map(([key, path]) => [key.toLowerCase(), path]));
  let applied = 0;

  for (const [key, message] of Object.entries(problemFieldErrors(error))) {
    const normalized = key.replace(/^\$\./, "");
    const path = paths[normalized] ?? lower.get(normalized.toLowerCase());
    if (!path) continue;

    setError(path, { type: "server", message }, { shouldFocus: applied === 0 });
    applied++;
  }

  return applied;
}
