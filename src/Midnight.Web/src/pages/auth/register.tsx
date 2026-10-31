import { api } from "@/api/client";
import type { LoginResponse } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { AuthHeader } from "@/components/shared/auth-header";
import { EmptyState } from "@/components/shared/empty-state";
import { PasswordInput } from "@/components/shared/password-input";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { InfoCircleIcon, LockIcon } from "@/icons";
import { notifyError } from "@/lib/notify";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";

interface RegisterForm {
  username: string;
  email: string;
  password: string;
  repeatedPassword: string;
}

export default function RegisterPage() {
  const { t } = useT();
  const { settings, completeLogin, reloadSettings } = useAuth();
  const navigate = useNavigate();
  const [busy, setBusy] = useState(false);

  const { register, handleSubmit, getValues, formState: { errors } } = useForm<RegisterForm>({ mode: "onTouched" });

  if (!settings.registrationEnabled) {
    return (
      <EmptyState icon={<LockIcon />} title={t("Register.DisabledTitle")} description={t("Register.Disabled")}>
        <Button className="mt-6" render={<Link to="/login" />} nativeButton={false}>{t("Register.Login")}</Button>
      </EmptyState>
    );
  }

  const submit = async (values: RegisterForm) => {
    setBusy(true);

    try {
      const response = await api.post<LoginResponse>("/api/auth/register", { username: values.username, email: values.email, password: values.password });
      await completeLogin(response);
      await reloadSettings();
      navigate("/", { replace: true });
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <AuthHeader title={t("Register.Title")} description={t("Register.Description")} />

      {settings.setupRequired && (
        <Alert variant="info" className="mb-5 px-4 py-3">
          <InfoCircleIcon />
          <AlertDescription>{t("Register.SetupHint")}</AlertDescription>
        </Alert>
      )}

      <form onSubmit={handleSubmit(submit)} className="space-y-5" noValidate>
        <Field data-invalid={Boolean(errors.username)}>
          <FieldLabel className="mb-1.5 block">{t("Common.Username")} <span className="text-red-500">*</span></FieldLabel>
          <Input autoComplete="username" aria-invalid={errors.username ? "true" : undefined}
                 {...register("username", { pattern: { value: /^[a-zA-Z0-9_.-]{3,32}$/, message: t("Validation.Username") }, required: t("Validation.Username") })} />
          <FieldError errors={[errors.username]} />
        </Field>

        <Field data-invalid={Boolean(errors.email)}>
          <FieldLabel className="mb-1.5 block">{t("Common.Email")} <span className="text-red-500">*</span></FieldLabel>
          <Input type="email" autoComplete="email" aria-invalid={errors.email ? "true" : undefined}
                 {...register("email", { required: t("Validation.Email"), pattern: { value: /^\S+@\S+$/, message: t("Validation.Email") } })} />
          <FieldError errors={[errors.email]} />
        </Field>

        <Field data-invalid={Boolean(errors.password)}>
          <FieldLabel className="mb-1.5 block">{t("Common.Password")} <span className="text-red-500">*</span></FieldLabel>
          <PasswordInput autoComplete="new-password" aria-invalid={errors.password ? "true" : undefined}
                         {...register("password", { minLength: { value: 8, message: t("Security.PasswordHint") }, required: t("Security.PasswordHint") })} />
          <FieldError errors={[errors.password]} />
        </Field>

        <Field data-invalid={Boolean(errors.repeatedPassword)}>
          <FieldLabel className="mb-1.5 block">{t("Register.RepeatPassword")} <span className="text-red-500">*</span></FieldLabel>
          <PasswordInput autoComplete="new-password" aria-invalid={errors.repeatedPassword ? "true" : undefined}
                         {...register("repeatedPassword", { validate: (value) => value === getValues("password") || t("Register.PasswordMismatch") })} />
          <FieldError errors={[errors.repeatedPassword]} />
        </Field>

        <Button className="w-full" size="lg" type="submit" disabled={busy}>{t("Register.Submit")}</Button>
      </form>

      <p className="mt-5 text-center text-sm text-gray-700 sm:text-start dark:text-gray-400">
        {t("Register.HasAccount")}{" "}
        <Link to="/login" className="text-brand-500 hover:text-brand-600 dark:text-brand-400">{t("Register.Login")}</Link>
      </p>
    </div>
  );
}
