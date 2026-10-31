import { api } from "@/api/client";
import type { Session } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { tableStyles } from "@/components/shared/data-table";
import { EmptyState } from "@/components/shared/empty-state";
import { PasswordInput } from "@/components/shared/password-input";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Table, TableBody, TableCell, TableHeader, TableRow } from "@/components/ui/table";
import { useT } from "@/i18n";
import { EyeIcon, FloppyDiscIcon, LogOutIcon, SystemIcon } from "@/icons";
import { useConfirm } from "@/lib/confirm";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate } from "react-router";
import { TwoFactorCard } from "./two-factor-card";

interface PasswordForm {
  currentPassword: string;
  newPassword: string;
  repeatedPassword: string;
}

export default function SecurityPage() {
  const { isImpersonating, logout } = useAuth();
  const { t, formatDate } = useT();
  const confirm = useConfirm();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [busy, setBusy] = useState(false);

  const sessions = useQuery({ queryKey: ["account", "sessions"], queryFn: () => api.get<Session[]>("/api/account/sessions"), enabled: !isImpersonating });
  const { register, handleSubmit, reset, getValues, formState: { errors } } = useForm<PasswordForm>({ mode: "onTouched" });

  if (isImpersonating) return <EmptyState icon={<EyeIcon />} title={t("Nav.Security")} description={t("Security.ImpersonationHint")} />;

  const changePassword = async (values: PasswordForm) => {
    setBusy(true);

    try {
      await api.put("/api/account/password", { currentPassword: values.currentPassword, newPassword: values.newPassword });
      reset();
      notifySuccess(t("Security.PasswordChanged"));
      await queryClient.invalidateQueries({ queryKey: ["account", "sessions"] });
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const revoke = async (id: number) => {
    try {
      await api.delete(`/api/account/sessions/${id}`);
      await queryClient.invalidateQueries({ queryKey: ["account", "sessions"] });
    } catch (error) {
      notifyError(error);
    }
  };

  const logoutEverywhere = async () => {
    if (!(await confirm({ title: t("Security.LogoutEverywhere"), message: t("Security.LogoutEverywhereConfirm"), confirmLabel: t("Security.LogoutEverywhere"), cancelLabel: t("Common.Cancel") })))
      return;

    try {
      await api.delete("/api/account/sessions");
      await logout();
      navigate("/login");
    } catch (error) {
      notifyError(error);
    }
  };

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
          <h4 className="mb-6 text-lg font-semibold text-gray-800 dark:text-white/90">{t("Security.ChangePassword")}</h4>
          <form onSubmit={handleSubmit(changePassword)} className="space-y-5" noValidate>
            <Field data-invalid={Boolean(errors.currentPassword)}>
              <FieldLabel className="mb-1.5 block">{t("Security.CurrentPassword")}</FieldLabel>
              <PasswordInput autoComplete="current-password" aria-invalid={errors.currentPassword ? "true" : undefined}
                             {...register("currentPassword", { required: t("Validation.Required") })} />
              <FieldError errors={[errors.currentPassword]} />
            </Field>
            <Field data-invalid={Boolean(errors.newPassword)}>
              <FieldLabel className="mb-1.5 block">{t("Security.NewPassword")}</FieldLabel>
              <PasswordInput autoComplete="new-password" aria-invalid={errors.newPassword ? "true" : undefined}
                             {...register("newPassword", { minLength: { value: 8, message: t("Security.PasswordHint") }, required: t("Security.PasswordHint") })} />
              <FieldDescription>{t("Security.PasswordHint")}</FieldDescription>
              <FieldError errors={[errors.newPassword]} />
            </Field>
            <Field data-invalid={Boolean(errors.repeatedPassword)}>
              <FieldLabel className="mb-1.5 block">{t("Register.RepeatPassword")}</FieldLabel>
              <PasswordInput autoComplete="new-password" aria-invalid={errors.repeatedPassword ? "true" : undefined}
                             {...register("repeatedPassword", { validate: (value) => value === getValues("newPassword") || t("Register.PasswordMismatch") })} />
              <FieldError errors={[errors.repeatedPassword]} />
            </Field>
            <div className="flex justify-end">
              <Button type="submit" disabled={busy}>
                <FloppyDiscIcon />
                {t("Common.SaveChanges")}
              </Button>
            </div>
          </form>
        </div>

        <TwoFactorCard />
      </div>

      <div className="overflow-hidden rounded-2xl border border-gray-200 bg-white dark:border-gray-800 dark:bg-white/[0.02]">
        <div className="flex flex-wrap items-center justify-between gap-3 border-b border-gray-100 px-5 py-4 sm:px-6 dark:border-gray-800">
          <h3 className="text-lg font-semibold text-gray-800 dark:text-white/90">{t("Security.Sessions")}</h3>
          <Button variant="outline" size="sm" className="text-red-500" onClick={logoutEverywhere}>
            <LogOutIcon />
            {t("Security.LogoutEverywhere")}
          </Button>
        </div>
        <div className="max-w-full overflow-x-auto">
          <Table>
            <TableHeader className="border-b border-gray-100 dark:border-white/5">
              <TableRow>
                <TableCell isHeader className={tableStyles.headerCell}>{t("Security.Device")}</TableCell>
                <TableCell isHeader className={tableStyles.headerCell}>{t("Security.IpAddress")}</TableCell>
                <TableCell isHeader className={tableStyles.headerCell}>{t("Security.LastUsed")}</TableCell>
                <TableCell isHeader className={tableStyles.headerCell}>{" "}</TableCell>
              </TableRow>
            </TableHeader>
            <TableBody className="divide-y divide-gray-100 dark:divide-white/5">
              {(sessions.data ?? []).map((session) => (
                <TableRow key={session.id}>
                  <TableCell className={tableStyles.bodyCell}>
                    <div className="flex items-center gap-3">
                      <SystemIcon className="size-5 shrink-0" />
                      <span className="max-w-md truncate text-gray-800 dark:text-white/90" title={session.userAgent ?? ""}>{session.userAgent ?? "-"}</span>
                      {session.isCurrent && <Badge size="sm">{t("Security.ThisDevice")}</Badge>}
                    </div>
                  </TableCell>
                  <TableCell className={`${tableStyles.bodyCell} font-mono`}>{session.ipAddress ?? "-"}</TableCell>
                  <TableCell className={tableStyles.bodyCell}>{formatDate(session.lastUsedAt)}</TableCell>
                  <TableCell className={`${tableStyles.bodyCell} text-end`}>
                    {!session.isCurrent && (
                      <Button variant="ghost" size="xs" className="text-red-500" onClick={() => revoke(session.id)}>{t("Security.Revoke")}</Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  );
}
