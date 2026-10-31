import { api } from "@/api/client";
import { useAuth } from "@/auth/AuthContext";
import { UserAvatar } from "@/components/shared/user-avatar";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useT } from "@/i18n";
import { FloppyDiscIcon, TrashBinIcon, UploadIcon } from "@/icons";
import { notifyError, notifySuccess } from "@/lib/notify";
import { useRef, useState } from "react";
import { useForm } from "react-hook-form";

const MAX_AVATAR_SIZE = 1024 * 1024;

interface ProfileForm {
  username: string;
  email: string;
}

export default function ProfilePage() {
  const { user, reload } = useAuth();
  const { t } = useT();
  const fileInput = useRef<HTMLInputElement>(null);
  const [busy, setBusy] = useState(false);
  const [avatarVersion, setAvatarVersion] = useState(0);

  const { register, handleSubmit, formState: { errors } } = useForm<ProfileForm>({
    mode: "onTouched",
    defaultValues: { username: user?.username ?? "", email: user?.email ?? "" },
  });

  if (!user) return null;

  const save = async (values: ProfileForm) => {
    setBusy(true);

    try {
      await api.put("/api/account/profile", values);
      await reload();
      notifySuccess(t("Profile.Saved"));
    } catch (error) {
      notifyError(error);
    } finally {
      setBusy(false);
    }
  };

  const upload = async (file: File | undefined) => {
    if (!file) return;

    if (file.size > MAX_AVATAR_SIZE) {
      notifyError(t("Profile.AvatarTooLarge"));
      return;
    }

    try {
      await api.upload("/api/account/avatar", file);
      setAvatarVersion((value) => value + 1);
      await reload();
      notifySuccess(t("Profile.AvatarUpdated"));
    } catch (error) {
      notifyError(error);
    }
  };

  const removeAvatar = async () => {
    try {
      await api.delete("/api/account/avatar");
      await reload();
    } catch (error) {
      notifyError(error);
    }
  };

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
      <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
        <h4 className="mb-6 text-lg font-semibold text-gray-800 dark:text-white/90">{t("Profile.Avatar")}</h4>
        <div className="flex flex-col items-center text-center">
          <UserAvatar userId={user.id} hasAvatar={user.hasAvatar} name={user.username} version={avatarVersion} className="size-28! text-3xl" />
          <p className="mt-4 text-sm text-gray-500 dark:text-gray-400">{t("Profile.AvatarHint")}</p>
          <input ref={fileInput} type="file" accept="image/png,image/jpeg,image/webp" className="hidden"
                 onChange={(event) => { void upload(event.target.files?.[0]); event.target.value = ""; }} />
          <div className="mt-5 flex gap-3">
            <Button variant="outline" size="sm" onClick={() => fileInput.current?.click()}>
              <UploadIcon />
              {t("Profile.Upload")}
            </Button>
            {user.hasAvatar && (
              <Button variant="ghost" size="sm" className="text-red-500 hover:text-red-600" onClick={removeAvatar}>
                <TrashBinIcon />
                {t("Common.Remove")}
              </Button>
            )}
          </div>
        </div>
      </div>

      <div className="rounded-2xl border border-gray-200 bg-white p-5 lg:col-span-2 lg:p-6 dark:border-gray-800 dark:bg-white/[0.02]">
        <h4 className="mb-6 text-lg font-semibold text-gray-800 dark:text-white/90">{t("Profile.Details")}</h4>
        <form onSubmit={handleSubmit(save)} className="space-y-5" noValidate>
          <div className="grid grid-cols-1 gap-5 sm:grid-cols-2">
            <Field data-invalid={Boolean(errors.username)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Username")}</FieldLabel>
              <Input aria-invalid={errors.username ? "true" : undefined}
                     {...register("username", { pattern: { value: /^[a-zA-Z0-9_.-]{3,32}$/, message: t("Validation.Username") }, required: t("Validation.Username") })} />
              <FieldError errors={[errors.username]} />
            </Field>
            <Field data-invalid={Boolean(errors.email)}>
              <FieldLabel className="mb-1.5 block">{t("Common.Email")}</FieldLabel>
              <Input type="email" aria-invalid={errors.email ? "true" : undefined}
                     {...register("email", { required: t("Validation.Email"), pattern: { value: /^\S+@\S+$/, message: t("Validation.Email") } })} />
              <FieldError errors={[errors.email]} />
            </Field>
          </div>
          <div className="flex justify-end">
            <Button type="submit" disabled={busy}>
              <FloppyDiscIcon />
              {t("Common.SaveChanges")}
            </Button>
          </div>
        </form>
      </div>
    </div>
  );
}
