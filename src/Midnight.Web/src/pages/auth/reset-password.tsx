import { api } from "@/api/client";
import { AuthHeader } from "@/components/shared/auth-header";
import { PasswordInput } from "@/components/shared/password-input";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { useT } from "@/i18n";
import { OctagonAlertIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { useNavigate, useSearchParams } from "react-router";

export default function ResetPasswordPage() {
  const { t } = useT();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const token = params.get("token");
  const [busy, setBusy] = useState(false);
  const { register, handleSubmit, formState: { errors } } = useForm<{ newPassword: string }>({ mode: "onTouched" });

  const submit = async (values: { newPassword: string }) => {
    setBusy(true);

    try {
      await api.post("/api/auth/password-reset/confirm", { token, newPassword: values.newPassword });
      notifySuccess(t("ResetPassword.Success"));
      navigate("/login");
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <AuthHeader title={t("ResetPassword.Title")} description={t("ResetPassword.Description")} />

      {!token ? (
        <Alert variant="destructive" className="px-4 py-3">
          <OctagonAlertIcon />
          <AlertDescription>{t("ResetPassword.InvalidLink")}</AlertDescription>
        </Alert>
      ) : (
        <form onSubmit={handleSubmit(submit)} className="space-y-5" noValidate>
          <Field data-invalid={Boolean(errors.newPassword)}>
            <FieldLabel className="mb-1.5 block">{t("ResetPassword.NewPassword")}</FieldLabel>
            <PasswordInput autoComplete="new-password" aria-invalid={errors.newPassword ? "true" : undefined}
                           {...register("newPassword", { minLength: { value: 8, message: t("Security.PasswordHint") }, required: t("Security.PasswordHint") })} />
            <FieldError errors={[errors.newPassword]} />
          </Field>
          <Button className="w-full" size="lg" type="submit" disabled={busy}>{t("ResetPassword.Submit")}</Button>
        </form>
      )}
    </div>
  );
}
