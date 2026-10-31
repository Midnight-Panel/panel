import { api } from "@/api/client";
import type { LoginResponse } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { AuthHeader } from "@/components/shared/auth-header";
import { PasswordInput } from "@/components/shared/password-input";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { InputOTP, InputOTPGroup, InputOTPSlot } from "@/components/ui/input-otp";
import { useT } from "@/i18n";
import { LockIcon } from "@/icons";
import { notifyError } from "@/lib/notify";
import { safeReturnUrl } from "@/lib/returnUrl";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router";

interface LoginForm {
  email: string;
  password: string;
}

export default function LoginPage() {
  const { t } = useT();
  const { settings, completeLogin } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [twoFactor, setTwoFactor] = useState(false);
  const [code, setCode] = useState("");
  const [busy, setBusy] = useState(false);

  const { register, handleSubmit, getValues, formState: { errors } } = useForm<LoginForm>({ mode: "onTouched" });

  if (settings.setupRequired) return <Navigate to="/register" replace />;

  const submit = async (values: LoginForm, twoFactorCode?: string) => {
    setBusy(true);

    try {
      const response = await api.post<LoginResponse>("/api/auth/login", { ...values, twoFactorCode: twoFactorCode ?? null });

      if (response.requiresTwoFactor) {
        setTwoFactor(true);
        return;
      }

      await completeLogin(response);
      navigate(safeReturnUrl(params.get("returnUrl")), { replace: true });
    } catch (error) {
      setCode("");
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  if (twoFactor) {
    return (
      <div>
        <AuthHeader title={t("Login.TwoFactorTitle")} description={t("Login.TwoFactorDescription")} />
        <Alert variant="info" className="mb-6 px-4 py-3">
          <LockIcon />
          <AlertDescription>{t("Login.TwoFactorHint")}</AlertDescription>
        </Alert>
        <div className="mb-6 flex justify-center">
          <InputOTP maxLength={6} value={code} onChange={setCode} autoFocus disabled={busy}
                    onComplete={(value: string) => submit(getValues(), value)}>
            <InputOTPGroup>
              {[0, 1, 2, 3, 4, 5].map((index) => <InputOTPSlot key={index} index={index} />)}
            </InputOTPGroup>
          </InputOTP>
        </div>
        <Button className="w-full" size="lg" disabled={busy || code.length !== 6} onClick={() => submit(getValues(), code)}>
          {t("Login.Submit")}
        </Button>
        <Button variant="ghost" className="mt-3 w-full" onClick={() => { setTwoFactor(false); setCode(""); }}>
          {t("Common.Back")}
        </Button>
      </div>
    );
  }

  return (
    <div>
      <AuthHeader title={t("Login.Title")} description={t("Login.Description")} />

      <form onSubmit={handleSubmit((values) => submit(values))} className="space-y-5" noValidate>
        <Field data-invalid={Boolean(errors.email)}>
          <FieldLabel className="mb-1.5 block">{t("Common.Email")} <span className="text-red-500">*</span></FieldLabel>
          <Input type="email" autoComplete="username" placeholder="info@example.com" aria-invalid={errors.email ? "true" : undefined}
                 {...register("email", { required: t("Validation.Email"), pattern: { value: /^\S+@\S+$/, message: t("Validation.Email") } })} />
          <FieldError errors={[errors.email]} />
        </Field>

        <Field data-invalid={Boolean(errors.password)}>
          <FieldLabel className="mb-1.5 block">{t("Common.Password")} <span className="text-red-500">*</span></FieldLabel>
          <PasswordInput autoComplete="current-password" aria-invalid={errors.password ? "true" : undefined}
                         {...register("password", { required: t("Validation.Required") })} />
          <FieldError errors={[errors.password]} />
        </Field>

        <div className="flex justify-end">
          <Link to="/forgot-password" className="text-sm text-brand-500 hover:text-brand-600 dark:text-brand-400">
            {t("Login.ForgotPassword")}
          </Link>
        </div>

        <Button className="w-full" size="lg" type="submit" disabled={busy}>{t("Login.Submit")}</Button>
      </form>

      {settings.registrationEnabled && (
        <p className="mt-5 text-center text-sm text-gray-700 sm:text-start dark:text-gray-400">
          {t("Login.NoAccount")}{" "}
          <Link to="/register" className="text-brand-500 hover:text-brand-600 dark:text-brand-400">{t("Login.Register")}</Link>
        </p>
      )}
    </div>
  );
}
