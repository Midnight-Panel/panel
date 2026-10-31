import { api, toQueryString } from "@/api/client";
import type { Allocation, CreateAllocationsResponse, PagedResult } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { DataTable, type DataTableColumn } from "@/components/shared/data-table";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { PlusIcon, TrashBinIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { Permissions } from "@/lib/permissions";
import { useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useNode } from "./layout";

interface CreateForm {
  ipAddress: string;
  ports: string;
  notes: string;
}

export default function NodeAllocationsPage() {
  const node = useNode();
  const { t } = useT();
  const { can } = useAuth();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [busy, setBusy] = useState(false);
  const [selected, setSelected] = useState<number[]>([]);
  const canWrite = can(Permissions.AdminNodesWrite);

  const { register, handleSubmit, reset, formState: { errors } } = useForm<CreateForm>({ mode: "onTouched", defaultValues: { ipAddress: "0.0.0.0", ports: "", notes: "" } });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["admin", "nodes", node.id, "allocations"] });

  const remove = async (ids: number[]) => {
    if (!(await confirm({ title: t("Common.Delete"), message: t("Allocations.DeleteConfirm", ids.length), confirmLabel: t("Common.Delete"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.post(`/api/admin/nodes/${node.id}/allocations/delete`, { ids });
      setSelected([]);
      await refresh();
    } catch (error) {
      notifyError(error);
    }
  };

  const create = async (values: CreateForm) => {
    setBusy(true);

    try {
      const result = await api.post<CreateAllocationsResponse>(`/api/admin/nodes/${node.id}/allocations`, values);
      notifySuccess(t("Allocations.Created", result.created, result.skipped));
      setOpen(false);
      await refresh();
      await queryClient.invalidateQueries({ queryKey: ["admin", "nodes", node.id] });
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const toggle = (id: number, checked: boolean) => setSelected((value) => (checked ? [...value, id] : value.filter((x) => x !== id)));

  const columns: DataTableColumn<Allocation>[] = [
    ...(canWrite
      ? [{
          key: "select", title: "",
          render: (a: Allocation) => <Checkbox checked={selected.includes(a.id)} disabled={a.serverId != null} onCheckedChange={(checked) => toggle(a.id, Boolean(checked))} />,
        }]
      : []),
    { key: "ip", title: t("Allocations.IpAddress"), sortKey: "ipAddress", render: (a) => <span className="font-mono text-gray-800 dark:text-white/90">{a.ipAddress}</span> },
    { key: "port", title: t("Allocations.Port"), sortKey: "port", render: (a) => <span className="font-mono">{a.port}</span> },
    { key: "notes", title: t("Allocations.Notes"), render: (a) => a.notes ?? "-" },
    {
      key: "server", title: t("Allocations.Server"),
      render: (a) => (a.serverId != null ? <Badge size="sm">#{a.serverId}</Badge> : <Badge size="sm" color="gray">{t("Allocations.Free")}</Badge>),
    },
    ...(canWrite
      ? [{
          key: "actions", title: "", align: "end" as const,
          render: (a: Allocation) => a.serverId == null && (
            <Button variant="ghost" size="icon-sm" className="text-red-500" onClick={() => remove([a.id])} aria-label={t("Common.Delete")}><TrashBinIcon /></Button>
          ),
        }]
      : []),
  ];

  return (
    <>
      <DataTable
        queryKey={["admin", "nodes", node.id, "allocations"]}
        load={(query) => api.get<PagedResult<Allocation>>(`/api/admin/nodes/${node.id}/allocations?${toQueryString(query)}`)}
        columns={columns}
        rowKey={(a) => a.id}
        title={t("Nodes.Allocations")}
        description={t("Allocations.Description")}
        pageSize={50}
        toolbar={canWrite && (
          <div className="flex gap-2">
            {selected.length > 0 && (
              <Button variant="outline" className="text-red-500" onClick={() => remove(selected)}>
                <TrashBinIcon />
                {t("Allocations.DeleteSelected", selected.length)}
              </Button>
            )}
            <Button onClick={() => { reset(); setOpen(true); }}>
              <PlusIcon />
              {t("Allocations.Create")}
            </Button>
          </div>
        )}
      />

      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>{t("Allocations.Create")}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit(create)} className="space-y-5" noValidate>
            <Field data-invalid={Boolean(errors.ipAddress)}>
              <FieldLabel className="mb-1.5 block">{t("Allocations.IpAddress")}</FieldLabel>
              <Input className="font-mono" aria-invalid={errors.ipAddress ? "true" : undefined} {...register("ipAddress", { required: t("Validation.Required") })} />
              <FieldDescription>{t("Allocations.IpHint")}</FieldDescription>
              <FieldError errors={[errors.ipAddress]} />
            </Field>
            <Field data-invalid={Boolean(errors.ports)}>
              <FieldLabel className="mb-1.5 block">{t("Allocations.Ports")}</FieldLabel>
              <Input className="font-mono" placeholder="25565, 25570-25600" aria-invalid={errors.ports ? "true" : undefined}
                     {...register("ports", { required: t("Validation.Required"), pattern: { value: /^[\d\s,;-]+$/, message: t("Allocations.PortsHint") } })} />
              <FieldDescription>{t("Allocations.PortsHint")}</FieldDescription>
              <FieldError errors={[errors.ports]} />
            </Field>
            <Field>
              <FieldLabel className="mb-1.5 block">{t("Allocations.Notes")}</FieldLabel>
              <Input {...register("notes")} />
            </Field>
            <DialogFooter className="mx-0 mb-0 p-0">
              <Button type="button" variant="outline" onClick={() => setOpen(false)}>{t("Common.Cancel")}</Button>
              <Button type="submit" disabled={busy}>{t("Allocations.Create")}</Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
