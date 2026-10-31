import { api } from "@/api/client";
import type { TwoFactorSetup } from "@/api/types";
import { useAuth } from "@/auth/AuthContext";
import { PasswordInput } from "@/components/shared/password-input";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { FieldLabel } from "@/components/ui/field";
import { InputOTP, InputOTPGroup, InputOTPSlot } from "@/components/ui/input-otp";
import { useT } from "@/i18n";
import { CheckCircleIcon, LockIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { cn } from "@/utils";
import { useState } from "react";

const apps = [
  ["Google Authenticator", "https://support.google.com/accounts/answer/1066447?hl=en"],
  ["Microsoft Authenticator", "https://www.microsoft.com/en-us/account/authenticator"],
  ["Authy", "https://authy.com/download/"],
  ["1Password", "https://support.1password.com/one-time-passwords/"],
];

function CodeInput({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  return (
    <InputOTP maxLength={6} value={value} onChange={onChange}>
      <InputOTPGroup>
        {[0, 1, 2, 3, 4, 5].map((index) => <InputOTPSlot key={index} index={index} />)}
      </InputOTPGroup>
    </InputOTP>
  );
}

export function TwoFactorCard() {
  const { user, reload } = useAuth();
  const { t } = useT();
  const [step, setStep] = useState(-1);
  const [setup, setSetup] = useState<TwoFactorSetup | null>(null);
  const [code, setCode] = useState("");
  const [password, setPassword] = useState("");
  const [busy, setBusy] = useState(false);

  const run = async (action: () => Promise<void>) => {
    setBusy(true);
    try {
      await action();
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const card = "h-full rounded-2xl border border-gray-200 bg-white p-5 lg:p-6 dark:border-gray-800 dark:bg-white/3";

  if (user?.twoFactorEnabled) {
    return (
      <div className={card}>
        <div className="flex flex-col items-center text-center">
          <div className="mb-4 flex size-16 items-center justify-center rounded-full bg-green-50 text-green-600 dark:bg-green-500/15 dark:text-green-500 [&>svg]:size-8">
            <CheckCircleIcon />
          </div>
          <h4 className="text-lg font-semibold text-gray-800 dark:text-white/90">{t("Security.TwoFactor")}</h4>
          <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">{t("Security.TwoFactorEnabledText")}</p>
        </div>
        <div className="mt-6 space-y-4">
          <div>
            <FieldLabel className="mb-1.5 block">{t("Common.Password")}</FieldLabel>
            <PasswordInput value={password} onChange={(event) => setPassword(event.target.value)} autoComplete="current-password" />
          </div>
          <div>
            <FieldLabel className="mb-1.5 block">{t("Login.TwoFactorTitle")}</FieldLabel>
            <CodeInput value={code} onChange={setCode} />
          </div>
          <Button variant="destructive" disabled={busy || !password || code.length !== 6}
                  onClick={() => run(async () => {
                    await api.post("/api/account/2fa/disable", { password, code });
                    setPassword("");
                    setCode("");
                    await reload();
                    notifySuccess(t("Security.TwoFactorDeactivated"));
                  })}>
            {t("Security.DisableTwoFactor")}
          </Button>
        </div>
      </div>
    );
  }

  if (step < 0) {
    return (
      <div className={cn(card, "flex flex-col items-center justify-center text-center")}>
        <div className="mb-4 flex size-16 items-center justify-center rounded-full bg-amber-50 text-amber-600 dark:bg-amber-500/15 dark:text-orange-400 [&>svg]:size-8">
          <LockIcon />
        </div>
        <h4 className="text-lg font-semibold text-gray-800 dark:text-white/90">{t("Security.TwoFactor")}</h4>
        <p className="mt-2 max-w-sm text-sm text-gray-500 dark:text-gray-400">{t("Security.TwoFactorDisabledText")}</p>
        <Button className="mt-6" onClick={() => setStep(0)}>{t("Security.EnableTwoFactor")}</Button>
      </div>
    );
  }

  const steps = [t("Security.StepApp"), t("Security.StepScan")];

  return (
    <div className={card}>
      <h4 className="mb-5 text-lg font-semibold text-gray-800 dark:text-white/90">{t("Security.TwoFactor")}</h4>

      <ol className="mb-6 flex items-center gap-3">
        {steps.map((label, index) => (
          <li key={label} className="flex items-center gap-2 text-sm">
            <span className={cn(
              "flex size-7 items-center justify-center rounded-full text-xs font-semibold",
              index <= step ? "bg-brand-500 text-white" : "bg-gray-100 text-gray-500 dark:bg-gray-800 dark:text-gray-400",
            )}>{index + 1}</span>
            <span className={index <= step ? "font-medium text-gray-800 dark:text-white/90" : "text-gray-500 dark:text-gray-400"}>{label}</span>
            {index < steps.length - 1 && <span className="mx-1 h-px w-8 bg-gray-200 dark:bg-gray-800" />}
          </li>
        ))}
      </ol>

      {step === 0 && (
        <div className="space-y-4">
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("Security.InstallApp")}</p>
          <ul className="space-y-2">
            {apps.map(([name, url]) => (
              <li key={name}>
                <a href={url} target="_blank" rel="noreferrer" className="flex items-center gap-2 text-sm text-brand-500 hover:text-brand-600 dark:text-brand-400">
                  <span className="size-1.5 rounded-full bg-brand-500" />
                  {name}
                </a>
              </li>
            ))}
          </ul>
          <div className="flex justify-between pt-2">
            <Button variant="outline" onClick={() => setStep(-1)}>{t("Common.Cancel")}</Button>
            <Button disabled={busy} onClick={() => run(async () => {
              setSetup(await api.post<TwoFactorSetup>("/api/account/2fa/setup"));
              setStep(1);
            })}>{t("Common.Continue")}</Button>
          </div>
        </div>
      )}

      {step === 1 && setup && (
        <div className="flex flex-col items-center gap-4 text-center">
          <p className="text-sm text-gray-500 dark:text-gray-400">{t("Security.ScanQrCode")}</p>
          <img src={setup.qrCode} alt="QR Code" className="size-44 rounded-xl bg-white p-2" />
          <Alert variant="warning" className="px-4 py-3 text-start">
            <AlertDescription>
              {t("Security.ManualKey")}
              <code className="mt-1 block font-mono text-xs break-all text-gray-800 dark:text-white/90">{setup.secret}</code>
            </AlertDescription>
          </Alert>
          <CodeInput value={code} onChange={setCode} />
          <Button disabled={busy || code.length !== 6} onClick={() => run(async () => {
            await api.post("/api/account/2fa/enable", { code });
            setCode("");
            setSetup(null);
            setStep(-1);
            await reload();
            notifySuccess(t("Security.TwoFactorActivated"));
          })}>{t("Common.Continue")}</Button>
        </div>
      )}
    </div>
  );
}
