import { api } from "@/api/client";
import { AuthHeader } from "@/components/shared/auth-header";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { CheckCircleIcon, ChevronLeftIcon } from "@/icons";
import { notifyError } from "@/lib/notify";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link } from "react-router";

export default function ForgotPasswordPage() {
  const { t } = useT();
  const [busy, setBusy] = useState(false);
  const [sent, setSent] = useState(false);
  const { register, handleSubmit, formState: { errors } } = useForm<{ email: string }>({ mode: "onTouched" });

  const submit = async (values: { email: string }) => {
    setBusy(true);

    try {
      await api.post("/api/auth/password-reset", values);
      setSent(true);
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <Link to="/login" className="mb-6 inline-flex items-center text-sm text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
        <ChevronLeftIcon />
        {t("ForgotPassword.BackToLogin")}
      </Link>
      <AuthHeader title={t("ForgotPassword.Title")} description={t("ForgotPassword.Description")} />

      {sent ? (
        <Alert variant="success" className="px-4 py-3">
          <CheckCircleIcon />
          <AlertDescription>{t("ForgotPassword.Sent")}</AlertDescription>
        </Alert>
      ) : (
        <form onSubmit={handleSubmit(submit)} className="space-y-5" noValidate>
          <Field data-invalid={Boolean(errors.email)}>
            <FieldLabel className="mb-1.5 block">{t("Common.Email")}</FieldLabel>
            <Input type="email" autoComplete="email" aria-invalid={errors.email ? "true" : undefined}
                   {...register("email", { required: t("Validation.Email"), pattern: { value: /^\S+@\S+$/, message: t("Validation.Email") } })} />
            <FieldError errors={[errors.email]} />
          </Field>
          <Button className="w-full" size="lg" type="submit" disabled={busy}>{t("ForgotPassword.Submit")}</Button>
        </form>
      )}
    </div>
  );
}
