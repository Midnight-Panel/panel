import { Button } from "@/components/ui/button";
import { useT } from "@/i18n";
import { Link } from "react-router";

export default function NotFoundPage() {
  const { t } = useT();

  return (
    <div className="flex flex-col items-center justify-center rounded-2xl border border-gray-200 bg-white px-6 py-16 text-center dark:border-gray-800 dark:bg-white/3">
      <img src="/images/error/404.svg" alt="404" className="w-full max-w-sm dark:hidden" />
      <img src="/images/error/404-dark.svg" alt="404" className="hidden w-full max-w-sm dark:block" />
      <h3 className="mt-8 text-title-sm font-semibold text-gray-800 dark:text-white/90">{t("NotFound.Title")}</h3>
      <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">{t("NotFound.Description")}</p>
      <Button className="mt-6" render={<Link to="/" />} nativeButton={false}>{t("NotFound.Back")}</Button>
    </div>
  );
}
